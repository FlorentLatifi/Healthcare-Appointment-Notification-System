using FluentAssertions;
using Healthcare.Adapters.Events;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Adapters.Services;
using Healthcare.Application.Ports.Events;
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
            outboxMessage.RetryCount.Should().Be(0);
            outboxMessage.Payload.Should().NotBeNullOrEmpty();

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
        services.AddSingleton<HealthcareDbContext>(sp =>
        {
            var ctx = new HealthcareDbContext(options, outboxSettings);
            ctx.Database.EnsureCreated();
            return ctx;
        });
        services.AddSingleton(outboxSettings);
        services.AddSingleton<IDomainEventDispatcher>(sp =>
            new DomainEventDispatcher(sp, sp.GetRequiredService<ILogger<DomainEventDispatcher>>()));
        services.AddSingleton<ILogger<DomainEventDispatcher>>(sp =>
            LoggerFactory.Create(b => { }).CreateLogger<DomainEventDispatcher>());
        services.AddSingleton<ILogger<OutboxRelayService>>(sp =>
            LoggerFactory.Create(b => { }).CreateLogger<OutboxRelayService>());
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var appointment = CreateConfirmedAppointment();

        await using (var writeCtx = new HealthcareDbContext(options, outboxSettings))
        {
            await writeCtx.Database.EnsureCreatedAsync();

            var domainEvent = appointment.DomainEvents.First();
            var outboxMessage = new OutboxMessage(
                domainEvent.GetType().AssemblyQualifiedName!,
                JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                DateTime.UtcNow);

            writeCtx.OutboxMessages.Add(outboxMessage);
            await writeCtx.SaveChangesAsync();
        }

        var relayLogger = provider.GetRequiredService<ILogger<OutboxRelayService>>();
        var relay = new OutboxRelayService(scopeFactory, relayLogger, outboxSettings);
        await InvokeProcessBatchAsync(relay);

        await using (var readCtx = new HealthcareDbContext(options, outboxSettings))
        {
            var processed = await readCtx.OutboxMessages.FirstAsync();
            processed.ProcessedAt.Should().NotBeNull();
            processed.Error.Should().BeNull();
        }
    }

    [Fact]
    public async Task OutboxRelayService_ExhaustedRetries_LogsError()
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
        loggerMock.Setup(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("permanently failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Verifiable();

        var services = new ServiceCollection();
        services.AddSingleton<HealthcareDbContext>(sp =>
        {
            var ctx = new HealthcareDbContext(options, outboxSettings);
            ctx.Database.EnsureCreated();
            return ctx;
        });
        services.AddSingleton(outboxSettings);
        services.AddSingleton<IDomainEventDispatcher>(sp =>
            new DomainEventDispatcher(sp, sp.GetRequiredService<ILogger<DomainEventDispatcher>>()));
        services.AddSingleton<ILogger<DomainEventDispatcher>>(sp =>
            LoggerFactory.Create(b => { }).CreateLogger<DomainEventDispatcher>());
        services.AddSingleton<ILogger<OutboxRelayService>>(sp => loggerMock.Object);

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        await using (var writeCtx = new HealthcareDbContext(options, outboxSettings))
        {
            await writeCtx.Database.EnsureCreatedAsync();

            var appointment = CreateConfirmedAppointment();
            var domainEvent = appointment.DomainEvents.First();
            var outboxMessage = new OutboxMessage(
                domainEvent.GetType().AssemblyQualifiedName!,
                "{bad json}", // will fail deserialization
                DateTime.UtcNow);
            outboxMessage.RetryCount = outboxSettings.MaxRetryAttempts - 1; // one more failure = exhaustion

            writeCtx.OutboxMessages.Add(outboxMessage);
            await writeCtx.SaveChangesAsync();
        }

        var relayLogger = provider.GetRequiredService<ILogger<OutboxRelayService>>();
        var relay = new OutboxRelayService(scopeFactory, relayLogger, outboxSettings);
        await InvokeProcessBatchAsync(relay);

        loggerMock.Verify();
    }

    [Fact]
    public async Task OutboxRelayService_HandlerFailure_IncrementsRetryCount()
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
        services.AddSingleton<HealthcareDbContext>(sp =>
        {
            var ctx = new HealthcareDbContext(options, outboxSettings);
            ctx.Database.EnsureCreated();
            return ctx;
        });
        services.AddSingleton(outboxSettings);
        services.AddSingleton<IDomainEventDispatcher>(sp =>
            new DomainEventDispatcher(sp, sp.GetRequiredService<ILogger<DomainEventDispatcher>>()));
        services.AddSingleton<ILogger<DomainEventDispatcher>>(sp =>
            LoggerFactory.Create(b => { }).CreateLogger<DomainEventDispatcher>());
        services.AddSingleton<ILogger<OutboxRelayService>>(sp =>
            LoggerFactory.Create(b => { }).CreateLogger<OutboxRelayService>());
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        await using (var writeCtx = new HealthcareDbContext(options, outboxSettings))
        {
            await writeCtx.Database.EnsureCreatedAsync();

            var outboxMessage = new OutboxMessage(
                "NonExistent.Event.Type.ThatDoesNotExist, NonExistentAssembly",
                "{}",
                DateTime.UtcNow);

            writeCtx.OutboxMessages.Add(outboxMessage);
            await writeCtx.SaveChangesAsync();
        }

        var relayLogger = provider.GetRequiredService<ILogger<OutboxRelayService>>();
        var relay = new OutboxRelayService(scopeFactory, relayLogger, outboxSettings);
        await InvokeProcessBatchAsync(relay);

        await using (var readCtx = new HealthcareDbContext(options, outboxSettings))
        {
            var failed = await readCtx.OutboxMessages.FirstAsync();
            failed.ProcessedAt.Should().BeNull();
            failed.RetryCount.Should().Be(5);
            failed.Error.Should().NotBeNullOrEmpty();
        }
    }

    private static async Task InvokeProcessBatchAsync(OutboxRelayService relay)
    {
        var method = typeof(OutboxRelayService).GetMethod("ProcessBatchAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (method != null)
        {
            var task = (Task?)method.Invoke(relay, new object[] { CancellationToken.None });
            if (task != null)
                await task;
        }
    }
}
