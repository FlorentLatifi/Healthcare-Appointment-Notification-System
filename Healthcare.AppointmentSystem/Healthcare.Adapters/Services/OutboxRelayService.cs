using Healthcare.Adapters.Events;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Healthcare.Adapters.Services;

public sealed class OutboxRelayService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxRelayService> _logger;
    private readonly OutboxSettings _settings;

    public OutboxRelayService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxRelayService> logger,
        OutboxSettings settings)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = settings;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.UseOutboxForDomainEvents)
        {
            _logger.LogInformation("Outbox relay is disabled (UseOutboxForDomainEvents is false).");
            return;
        }

        _logger.LogInformation(
            "Outbox relay started (interval: {Interval}s, max retries: {MaxRetries})",
            _settings.RelayIntervalSeconds, _settings.MaxRetryAttempts);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox relay batch failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(_settings.RelayIntervalSeconds), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HealthcareDbContext>();
        var eventDispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();

        var pendingMessages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < _settings.MaxRetryAttempts)
            .OrderBy(m => m.OccurredOn)
            .Take(_settings.BatchSize)
            .ToListAsync(ct);

        if (pendingMessages.Count == 0)
            return;

        _logger.LogDebug("Processing {Count} pending outbox message(s)", pendingMessages.Count);

        foreach (var message in pendingMessages)
        {
            try
            {
                var eventType = Type.GetType(message.EventType);
                if (eventType == null)
                {
                    _logger.LogError(
                        "Outbox message {Id} permanently failed: cannot resolve event type '{EventType}'",
                        message.Id, message.EventType);
                    message.Error = $"Unknown type: {message.EventType}";
                    message.RetryCount = _settings.MaxRetryAttempts;
                    continue;
                }

                var domainEvent = (IDomainEvent)JsonSerializer.Deserialize(message.Payload, eventType)!;
                await eventDispatcher.DispatchAsync(new[] { domainEvent }, ct);

                message.ProcessedAt = DateTime.UtcNow;
                message.Error = null;

                _logger.LogDebug(
                    "Outbox message {Id} ({EventType}) processed successfully",
                    message.Id, eventType.Name);
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.Error = $"{ex.GetType().Name}: {ex.Message}";

                if (message.RetryCount >= _settings.MaxRetryAttempts)
                {
                    _logger.LogError(
                        ex,
                        "Outbox message {Id} permanently failed after {RetryCount} attempts (max: {MaxRetries})",
                        message.Id, message.RetryCount, _settings.MaxRetryAttempts);
                }
                else
                {
                    _logger.LogWarning(
                        ex,
                        "Outbox message {Id} failed (attempt {RetryCount}/{MaxRetries})",
                        message.Id, message.RetryCount, _settings.MaxRetryAttempts);
                }
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
