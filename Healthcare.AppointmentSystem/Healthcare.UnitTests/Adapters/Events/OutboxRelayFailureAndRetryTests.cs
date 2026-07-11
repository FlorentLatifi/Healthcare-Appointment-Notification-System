using FluentAssertions;
using Healthcare.Adapters.Background;
using Healthcare.Adapters.Events;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Adapters.Services;
using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Common;
using Healthcare.Domain.Entities;
using Healthcare.Domain.ValueObjects;
using Healthcare.UnitTests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace Healthcare.UnitTests.Adapters.Events;

/// <summary>
/// Production-critical outbox scenarios: retry exhaustion, backoff gating, stale claims, idempotent MessageId.
/// Failures here mean lost domain events or duplicate side effects under multi-instance deploy.
/// </summary>
public sealed class OutboxRelayFailureAndRetryTests
{
    private static OutboxRelayService CreateRelay(
        IServiceScopeFactory scopeFactory,
        OutboxSettings settings) =>
        new(
            scopeFactory,
            LoggerFactory.Create(_ => { }).CreateLogger<OutboxRelayService>(),
            settings,
            new OutboxMetrics(),
            new OutboxRelayHealthState(),
            new LoggingBackgroundWorkerAlert(
                LoggerFactory.Create(_ => { }).CreateLogger<LoggingBackgroundWorkerAlert>()));

    private static (IDomainEvent DomainEvent, string TypeName, string Payload) CreateEvent()
    {
        var doctor = TestDataBuilder.ADoctor().WithEmail("outbox.fail@test.com").WithLicense("LIC-OF-1").Build();
        var patient = TestDataBuilder.APatient().WithEmail("outbox.fail.p@test.com").Build();
        var appointment = Appointment.Create(
            patient, doctor,
            AppointmentTime.Create(DateTime.UtcNow.Date.AddDays(12).AddHours(10)),
            "Outbox failure scenario appointment",
            new AppointmentCodeGenerator());
        var domainEvent = appointment.DomainEvents.First();
        return (
            domainEvent,
            domainEvent.GetType().AssemblyQualifiedName!,
            JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));
    }

    [Fact]
    public async Task ProcessBatch_RetryableFailures_ExhaustToDeadLetter()
    {
        // Essential: after MaxRetryAttempts, message must leave the hot path (DLQ) so poison events
        // cannot block the relay forever.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<HealthcareDbContext>().UseSqlite(connection).Options;

        var settings = new OutboxSettings
        {
            UseOutboxForDomainEvents = true,
            MaxRetryAttempts = 3,
            BatchSize = 10,
            BaseRetryDelaySeconds = 0, // allow immediate re-attempt for the test
            MaxRetryDelaySeconds = 1
        };

        var dispatcher = new Mock<IDomainEventDispatcher>();
        dispatcher.Setup(d => d.DispatchStrictAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("transient handler failure"));

        var services = new ServiceCollection();
        services.AddSingleton(settings);
        services.AddScoped(_ => new HealthcareDbContext(options, settings));
        services.AddSingleton(dispatcher.Object);
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        await using (var ctx = new HealthcareDbContext(options, settings))
            await ctx.Database.EnsureCreatedAsync();

        var (domainEvent, typeName, payload) = CreateEvent();
        await using (var write = new HealthcareDbContext(options, settings))
        {
            write.OutboxMessages.Add(new OutboxMessage(typeName, payload, domainEvent.OccurredOn, domainEvent.EventId));
            await write.SaveChangesAsync();
        }

        var relay = CreateRelay(sp.GetRequiredService<IServiceScopeFactory>(), settings);

        // Force NextAttemptAt to now between attempts so backoff doesn't hide the message.
        for (var i = 0; i < 3; i++)
        {
            await using (var fix = new HealthcareDbContext(options, settings))
            {
                var msg = await fix.OutboxMessages.FirstAsync();
                // Reset due time via re-mark if pending with future next attempt
                if (msg.Status == OutboxMessageStatus.Pending && msg.NextAttemptAt > DateTime.UtcNow)
                {
                    // Use EF to set NextAttemptAt through raw update
                    await fix.Database.ExecuteSqlRawAsync(
                        "UPDATE OutboxMessages SET NextAttemptAt = {0} WHERE Id = {1}",
                        DateTime.UtcNow.AddMinutes(-1), msg.Id);
                }
            }

            await relay.ProcessBatchAsync(CancellationToken.None);
        }

        await using var read = new HealthcareDbContext(options, settings);
        var final = await read.OutboxMessages.FirstAsync();
        final.Status.Should().Be(OutboxMessageStatus.DeadLetter);
        final.RetryCount.Should().BeGreaterThanOrEqualTo(settings.MaxRetryAttempts);
        final.DeadLetteredAt.Should().NotBeNull();
        final.ProcessedAt.Should().BeNull();
        dispatcher.Verify(
            d => d.DispatchStrictAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(settings.MaxRetryAttempts));
    }

    [Fact]
    public async Task ProcessBatch_NotDueYet_SkipsMessage()
    {
        // Essential: backoff must actually delay reprocessing; otherwise we thundering-herd handlers.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<HealthcareDbContext>().UseSqlite(connection).Options;
        var settings = new OutboxSettings { UseOutboxForDomainEvents = true, MaxRetryAttempts = 5, BatchSize = 10 };

        var dispatcher = new Mock<IDomainEventDispatcher>();
        var services = new ServiceCollection();
        services.AddSingleton(settings);
        services.AddScoped(_ => new HealthcareDbContext(options, settings));
        services.AddSingleton(dispatcher.Object);
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        await using (var ctx = new HealthcareDbContext(options, settings))
            await ctx.Database.EnsureCreatedAsync();

        var (domainEvent, typeName, payload) = CreateEvent();
        await using (var write = new HealthcareDbContext(options, settings))
        {
            write.OutboxMessages.Add(new OutboxMessage(typeName, payload, domainEvent.OccurredOn, domainEvent.EventId));
            await write.SaveChangesAsync();
            await write.Database.ExecuteSqlRawAsync(
                "UPDATE OutboxMessages SET NextAttemptAt = {0}",
                DateTime.UtcNow.AddHours(2));
        }

        var handled = await CreateRelay(sp.GetRequiredService<IServiceScopeFactory>(), settings)
            .ProcessBatchAsync(CancellationToken.None);

        handled.Should().Be(0);
        dispatcher.Verify(
            d => d.DispatchStrictAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()),
            Times.Never);

        await using var read = new HealthcareDbContext(options, settings);
        var msg = await read.OutboxMessages.FirstAsync();
        msg.Status.Should().Be(OutboxMessageStatus.Pending);
        msg.RetryCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessBatch_ReleasesStaleProcessingClaim_ThenProcesses()
    {
        // Essential: crash mid-process must not strand messages in Processing forever.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<HealthcareDbContext>().UseSqlite(connection).Options;
        var settings = new OutboxSettings
        {
            UseOutboxForDomainEvents = true,
            MaxRetryAttempts = 5,
            BatchSize = 10,
            ProcessingLeaseSeconds = 30
        };

        var services = new ServiceCollection();
        services.AddSingleton(settings);
        services.AddScoped(_ => new HealthcareDbContext(options, settings));
        services.AddSingleton<IDomainEventDispatcher>(sp =>
            new DomainEventDispatcher(sp, sp.GetRequiredService<ILogger<DomainEventDispatcher>>()));
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        await using (var ctx = new HealthcareDbContext(options, settings))
            await ctx.Database.EnsureCreatedAsync();

        var (domainEvent, typeName, payload) = CreateEvent();
        int id;
        await using (var write = new HealthcareDbContext(options, settings))
        {
            var msg = new OutboxMessage(typeName, payload, domainEvent.OccurredOn, domainEvent.EventId);
            write.OutboxMessages.Add(msg);
            await write.SaveChangesAsync();
            id = msg.Id;
            // Simulate abandoned claim older than lease
            await write.Database.ExecuteSqlRawAsync(
                "UPDATE OutboxMessages SET Status = {0}, ProcessingStartedAt = {1} WHERE Id = {2}",
                (int)OutboxMessageStatus.Processing,
                DateTime.UtcNow.AddMinutes(-10),
                id);
        }

        var handled = await CreateRelay(sp.GetRequiredService<IServiceScopeFactory>(), settings)
            .ProcessBatchAsync(CancellationToken.None);

        handled.Should().Be(1);
        await using var read = new HealthcareDbContext(options, settings);
        var final = await read.OutboxMessages.FirstAsync(m => m.Id == id);
        final.Status.Should().Be(OutboxMessageStatus.Processed);
        final.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveChanges_DuplicateMessageId_IsRejectedByUniqueIndex()
    {
        // Essential: MessageId = domain EventId prevents duplicate outbox rows for the same event.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<HealthcareDbContext>().UseSqlite(connection).Options;
        var settings = new OutboxSettings { UseOutboxForDomainEvents = true };

        await using var ctx = new HealthcareDbContext(options, settings);
        await ctx.Database.EnsureCreatedAsync();

        var messageId = Guid.NewGuid();
        ctx.OutboxMessages.Add(new OutboxMessage("T", "{}", DateTime.UtcNow, messageId));
        await ctx.SaveChangesAsync();

        ctx.OutboxMessages.Add(new OutboxMessage("T2", "{}", DateTime.UtcNow, messageId));
        var act = async () => await ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
