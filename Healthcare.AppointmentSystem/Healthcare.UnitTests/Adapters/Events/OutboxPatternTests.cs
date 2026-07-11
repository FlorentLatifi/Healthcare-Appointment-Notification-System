using FluentAssertions;
using Healthcare.Adapters.Background;
using Healthcare.Adapters.Events;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Adapters.Services;
using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Common;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Events;
using Healthcare.Domain.ValueObjects;
using Healthcare.UnitTests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace Healthcare.UnitTests.Adapters.Events;

public sealed class OutboxPatternTests
{
    private static Appointment CreateConfirmedAppointment()
    {
        var doctor = TestDataBuilder.ADoctor()
            .WithEmail("outbox.doctor@test.com")
            .WithLicense("LIC-OUTBOX-01")
            .Build();
        var patient = TestDataBuilder.APatient()
            .WithEmail("outbox.patient@test.com")
            .Build();
        var appointment = Appointment.Create(
            patient, doctor,
            AppointmentTime.Create(DateTime.UtcNow.Date.AddDays(10).AddHours(10)),
            "Outbox test", new AppointmentCodeGenerator());
        appointment.ApplyPricingStrategy(
            doctor.ConsultationFee.Amount, doctor.ConsultationFee.Currency);
        appointment.Confirm();
        return appointment;
    }

    private static OutboxMetrics CreateMetrics() => new();

    private static OutboxRelayService CreateRelay(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxRelayService> logger,
        OutboxSettings settings) =>
        new(
            scopeFactory,
            logger,
            settings,
            CreateMetrics(),
            new OutboxRelayHealthState(),
            new LoggingBackgroundWorkerAlert(LoggerFactory.Create(_ => { }).CreateLogger<LoggingBackgroundWorkerAlert>()));

    [Fact]
    public async Task SaveChangesAsync_WithOutboxEnabled_WritesOutboxRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlite(connection)
            .Options;

        var outboxSettings = new OutboxSettings { UseOutboxForDomainEvents = true };
        var appointment = CreateConfirmedAppointment();
        var expectedEventIds = appointment.DomainEvents.Select(e => e.EventId).ToHashSet();

        await using (var context = new HealthcareDbContext(options, outboxSettings))
        {
            await context.Database.EnsureCreatedAsync();

            context.Appointments.Add(appointment);
            await context.SaveChangesAsync();

            var outboxMessages = await context.OutboxMessages.ToListAsync();
            outboxMessages.Should().HaveCount(2);
            outboxMessages.Should().Contain(m => m.EventType.Contains("AppointmentCreatedEvent"));
            outboxMessages.Should().Contain(m => m.EventType.Contains("AppointmentConfirmedEvent"));

            var outboxMessage = outboxMessages.First(m => m.EventType.Contains("AppointmentConfirmedEvent"));
            outboxMessage.ProcessedAt.Should().BeNull();
            outboxMessage.Status.Should().Be(OutboxMessageStatus.Pending);
            outboxMessage.RetryCount.Should().Be(0);
            outboxMessage.Payload.Should().NotBeNullOrEmpty();
            outboxMessage.MessageId.Should().NotBe(Guid.Empty);
            outboxMessages.Select(m => m.MessageId).Should().BeEquivalentTo(expectedEventIds);

            appointment.DomainEvents.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task SaveChangesAsync_WithOutboxDisabled_DoesNotWriteOutboxRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlite(connection)
            .Options;

        var outboxSettings = new OutboxSettings { UseOutboxForDomainEvents = false };
        var appointment = CreateConfirmedAppointment();

        await using (var context = new HealthcareDbContext(options, outboxSettings))
        {
            await context.Database.EnsureCreatedAsync();

            context.Appointments.Add(appointment);
            await context.SaveChangesAsync();

            var outboxCount = await context.OutboxMessages.CountAsync();
            outboxCount.Should().Be(0);
        }
    }

    [Fact]
    public async Task SaveChangesAsync_WithoutOutboxSettings_DoesNotWriteOutboxRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlite(connection)
            .Options;

        var appointment = CreateConfirmedAppointment();

        await using (var context = new HealthcareDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();

            context.Appointments.Add(appointment);
            await context.SaveChangesAsync();

            var outboxCount = await context.OutboxMessages.CountAsync();
            outboxCount.Should().Be(0);
        }
    }

    [Fact]
    public async Task OutboxRelayService_ProcessesPendingMessages()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlite(connection)
            .Options;

        var outboxSettings = new OutboxSettings
        {
            UseOutboxForDomainEvents = true,
            MaxRetryAttempts = 5,
            BatchSize = 50,
        };

        var services = new ServiceCollection();
        services.AddSingleton(outboxSettings);
        services.AddScoped(_ => new HealthcareDbContext(options, outboxSettings));
        services.AddSingleton<IDomainEventDispatcher>(sp =>
            new DomainEventDispatcher(sp, sp.GetRequiredService<ILogger<DomainEventDispatcher>>()));
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        // Ensure schema once on the shared connection
        await using (var bootstrap = new HealthcareDbContext(options, outboxSettings))
            await bootstrap.Database.EnsureCreatedAsync();

        var appointment = CreateConfirmedAppointment();
        var domainEvent = appointment.DomainEvents.First();

        await using (var writeCtx = new HealthcareDbContext(options, outboxSettings))
        {
            var outboxMessage = new OutboxMessage(
                domainEvent.GetType().AssemblyQualifiedName!,
                JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                domainEvent.OccurredOn,
                domainEvent.EventId);

            writeCtx.OutboxMessages.Add(outboxMessage);
            await writeCtx.SaveChangesAsync();
        }

        var logger = provider.GetRequiredService<ILogger<OutboxRelayService>>();
        var relay = CreateRelay(scopeFactory, logger, outboxSettings);
        await relay.ProcessBatchAsync(CancellationToken.None);

        await using (var readCtx = new HealthcareDbContext(options, outboxSettings))
        {
            var processed = await readCtx.OutboxMessages.FirstAsync();
            processed.Status.Should().Be(OutboxMessageStatus.Processed);
            processed.ProcessedAt.Should().NotBeNull();
            processed.Error.Should().BeNull();
            processed.MessageId.Should().Be(domainEvent.EventId);
        }
    }

    [Fact]
    public async Task OutboxRelayService_NonRetryableBadJson_DeadLettersImmediately()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlite(connection)
            .Options;

        var outboxSettings = new OutboxSettings
        {
            UseOutboxForDomainEvents = true,
            MaxRetryAttempts = 3,
            BatchSize = 50,
        };

        var loggerMock = new Mock<ILogger<OutboxRelayService>>();

        var services = new ServiceCollection();
        services.AddSingleton(outboxSettings);
        services.AddScoped(_ => new HealthcareDbContext(options, outboxSettings));
        services.AddSingleton<IDomainEventDispatcher>(sp =>
            new DomainEventDispatcher(sp, sp.GetRequiredService<ILogger<DomainEventDispatcher>>()));
        services.AddSingleton<ILogger<OutboxRelayService>>(loggerMock.Object);
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        await using (var bootstrap = new HealthcareDbContext(options, outboxSettings))
            await bootstrap.Database.EnsureCreatedAsync();

        var appointment = CreateConfirmedAppointment();
        var domainEvent = appointment.DomainEvents.First();

        await using (var writeCtx = new HealthcareDbContext(options, outboxSettings))
        {
            var outboxMessage = new OutboxMessage(
                domainEvent.GetType().AssemblyQualifiedName!,
                "{bad json}",
                DateTime.UtcNow,
                domainEvent.EventId);

            writeCtx.OutboxMessages.Add(outboxMessage);
            await writeCtx.SaveChangesAsync();
        }

        var relay = CreateRelay(scopeFactory, loggerMock.Object, outboxSettings);
        await relay.ProcessBatchAsync(CancellationToken.None);

        await using (var readCtx = new HealthcareDbContext(options, outboxSettings))
        {
            var dead = await readCtx.OutboxMessages.FirstAsync();
            dead.Status.Should().Be(OutboxMessageStatus.DeadLetter);
            dead.DeadLetteredAt.Should().NotBeNull();
            dead.ProcessedAt.Should().BeNull();
            dead.Error.Should().NotBeNullOrEmpty();
        }

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("dead-lettered", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task OutboxRelayService_UnknownType_DeadLettersAsNonRetryable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlite(connection)
            .Options;

        var outboxSettings = new OutboxSettings
        {
            UseOutboxForDomainEvents = true,
            MaxRetryAttempts = 5,
            BatchSize = 50,
        };

        var services = new ServiceCollection();
        services.AddSingleton(outboxSettings);
        services.AddScoped(_ => new HealthcareDbContext(options, outboxSettings));
        services.AddSingleton<IDomainEventDispatcher>(sp =>
            new DomainEventDispatcher(sp, sp.GetRequiredService<ILogger<DomainEventDispatcher>>()));
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        await using (var bootstrap = new HealthcareDbContext(options, outboxSettings))
            await bootstrap.Database.EnsureCreatedAsync();

        await using (var writeCtx = new HealthcareDbContext(options, outboxSettings))
        {
            var outboxMessage = new OutboxMessage(
                "NonExistent.Event.Type.ThatDoesNotExist, NonExistentAssembly",
                "{}",
                DateTime.UtcNow,
                Guid.NewGuid());

            writeCtx.OutboxMessages.Add(outboxMessage);
            await writeCtx.SaveChangesAsync();
        }

        var logger = provider.GetRequiredService<ILogger<OutboxRelayService>>();
        var relay = CreateRelay(scopeFactory, logger, outboxSettings);
        await relay.ProcessBatchAsync(CancellationToken.None);

        await using (var readCtx = new HealthcareDbContext(options, outboxSettings))
        {
            var failed = await readCtx.OutboxMessages.FirstAsync();
            failed.Status.Should().Be(OutboxMessageStatus.DeadLetter);
            failed.ProcessedAt.Should().BeNull();
            failed.DeadLetteredAt.Should().NotBeNull();
            failed.RetryCount.Should().Be(outboxSettings.MaxRetryAttempts);
            failed.Error.Should().NotBeNullOrEmpty();
            failed.Error.Should().Contain("Unknown event type");
        }
    }

    [Fact]
    public async Task OutboxRelayService_HandlerFailure_SchedulesRetryWithBackoff()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlite(connection)
            .Options;

        var outboxSettings = new OutboxSettings
        {
            UseOutboxForDomainEvents = true,
            MaxRetryAttempts = 5,
            BatchSize = 50,
            BaseRetryDelaySeconds = 30,
            MaxRetryDelaySeconds = 300,
        };

        var dispatcherMock = new Mock<IDomainEventDispatcher>();
        dispatcherMock
            .Setup(d => d.DispatchStrictAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("handler blew up"));

        var services = new ServiceCollection();
        services.AddSingleton(outboxSettings);
        services.AddScoped(_ => new HealthcareDbContext(options, outboxSettings));
        services.AddSingleton(dispatcherMock.Object);
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        await using (var bootstrap = new HealthcareDbContext(options, outboxSettings))
            await bootstrap.Database.EnsureCreatedAsync();

        var appointment = CreateConfirmedAppointment();
        var domainEvent = appointment.DomainEvents.First();
        var before = DateTime.UtcNow;

        await using (var writeCtx = new HealthcareDbContext(options, outboxSettings))
        {
            writeCtx.OutboxMessages.Add(new OutboxMessage(
                domainEvent.GetType().AssemblyQualifiedName!,
                JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                domainEvent.OccurredOn,
                domainEvent.EventId));
            await writeCtx.SaveChangesAsync();
        }

        var logger = provider.GetRequiredService<ILogger<OutboxRelayService>>();
        var relay = CreateRelay(scopeFactory, logger, outboxSettings);
        await relay.ProcessBatchAsync(CancellationToken.None);

        await using (var readCtx = new HealthcareDbContext(options, outboxSettings))
        {
            var failed = await readCtx.OutboxMessages.FirstAsync();
            failed.Status.Should().Be(OutboxMessageStatus.Pending);
            failed.RetryCount.Should().Be(1);
            failed.ProcessedAt.Should().BeNull();
            failed.Error.Should().Contain("handler blew up");
            // Full jitter schedules NextAttemptAt in [now, now+baseDelay]
            failed.NextAttemptAt.Should().BeOnOrAfter(before);
            failed.NextAttemptAt.Should().BeOnOrBefore(before.AddSeconds(outboxSettings.BaseRetryDelaySeconds + 5));
        }
    }

    [Fact]
    public async Task OutboxMessage_MarkFailed_ExhaustsToDeadLetter()
    {
        var message = new OutboxMessage(
            "Some.Event, Assembly",
            "{}",
            DateTime.UtcNow,
            Guid.NewGuid());

        for (var i = 0; i < 4; i++)
        {
            message.MarkFailed(
                new InvalidOperationException($"fail-{i}"),
                maxRetryAttempts: 5,
                baseDelay: TimeSpan.FromSeconds(1),
                maxDelay: TimeSpan.FromMinutes(5));
            message.Status.Should().Be(OutboxMessageStatus.Pending);
        }

        message.MarkFailed(
            new InvalidOperationException("final"),
            maxRetryAttempts: 5,
            baseDelay: TimeSpan.FromSeconds(1),
            maxDelay: TimeSpan.FromMinutes(5));

        message.Status.Should().Be(OutboxMessageStatus.DeadLetter);
        message.RetryCount.Should().Be(5);
        message.DeadLetteredAt.Should().NotBeNull();
    }

    [Fact]
    public void WorkerHealthState_TracksSuccessAndFailure()
    {
        var health = new OutboxRelayHealthState();
        health.MarkStarted();
        health.MarkAttempt();
        health.MarkFailure(new InvalidOperationException("boom"));
        health.Snapshot().ConsecutiveFailures.Should().Be(1);
        health.MarkSuccess();
        var snap = health.Snapshot();
        snap.ConsecutiveFailures.Should().Be(0);
        snap.LastSuccessUtc.Should().NotBeNull();
        snap.LastError.Should().BeNull();
    }
}
