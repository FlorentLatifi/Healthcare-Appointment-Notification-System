using System.Diagnostics;
using System.Text.Json;
using Healthcare.Adapters.Events;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Healthcare.Adapters.Services;

/// <summary>
/// Background relay for the transactional outbox:
/// claim due messages → strict dispatch → mark processed / schedule retry / dead-letter.
/// </summary>
public sealed class OutboxRelayService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxRelayService> _logger;
    private readonly OutboxSettings _settings;
    private readonly OutboxMetrics _metrics;
    private readonly OutboxCircuitBreaker _circuitBreaker;

    public OutboxRelayService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxRelayService> logger,
        OutboxSettings settings,
        OutboxMetrics metrics)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = settings;
        _metrics = metrics;
        _circuitBreaker = new OutboxCircuitBreaker(
            settings.CircuitBreakerFailureThreshold,
            settings.CircuitBreakDuration);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.UseOutboxForDomainEvents)
        {
            _logger.LogInformation("Outbox relay is disabled (UseOutboxForDomainEvents is false).");
            return;
        }

        _logger.LogInformation(
            "Outbox relay started (interval={Interval}s, maxRetries={MaxRetries}, batch={Batch}, " +
            "backoff={BaseDelay}-{MaxDelay}s, lease={Lease}s, circuit={CircuitThreshold}/{CircuitBreak}s)",
            _settings.RelayIntervalSeconds,
            _settings.MaxRetryAttempts,
            _settings.BatchSize,
            _settings.BaseRetryDelaySeconds,
            _settings.MaxRetryDelaySeconds,
            _settings.ProcessingLeaseSeconds,
            _settings.CircuitBreakerFailureThreshold,
            _settings.CircuitBreakerBreakSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_circuitBreaker.TryEnter())
                {
                    _logger.LogWarning(
                        "Outbox circuit breaker is OPEN (state={State}); skipping batch",
                        _circuitBreaker.State);
                    await Task.Delay(_settings.CircuitBreakDuration, stoppingToken);
                    continue;
                }

                var sw = Stopwatch.StartNew();
                var processedCount = await ProcessBatchAsync(stoppingToken);
                sw.Stop();

                _metrics.RecordBatch(processedCount, sw.Elapsed.TotalMilliseconds, success: true);
                _circuitBreaker.RecordSuccess();

                _logger.LogDebug(
                    "Outbox batch completed in {ElapsedMs}ms (claimed/processed loop size context={Count})",
                    sw.ElapsedMilliseconds,
                    processedCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _circuitBreaker.RecordFailure();
                if (_circuitBreaker.IsOpen)
                    _metrics.RecordCircuitOpen();

                _metrics.RecordBatch(0, 0, success: false);
                _logger.LogError(
                    ex,
                    "Outbox relay batch failed (circuit={State})",
                    _circuitBreaker.State);
            }

            await Task.Delay(TimeSpan.FromSeconds(_settings.RelayIntervalSeconds), stoppingToken);
        }
    }

    /// <summary>
    /// Processes one batch. Internal for unit tests via reflection.
    /// </summary>
    internal async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HealthcareDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();

        var now = DateTime.UtcNow;

        // Recover claims stuck in Processing past lease timeout.
        await ReleaseStaleClaimsAsync(dbContext, now, ct);

        // AsNoTracking so ExecuteUpdate claim is not shadowed by a stale tracked entity.
        var candidateIds = await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(m => m.Status == OutboxMessageStatus.Pending && m.NextAttemptAt <= now)
            .OrderBy(m => m.OccurredOn)
            .Select(m => m.Id)
            .Take(_settings.BatchSize)
            .ToListAsync(ct);

        _metrics.SetPendingEstimate(candidateIds.Count);

        if (candidateIds.Count == 0)
            return 0;

        _logger.LogInformation(
            "Outbox claiming up to {Count} due message(s) (batchSize={BatchSize})",
            candidateIds.Count,
            _settings.BatchSize);

        var handled = 0;

        foreach (var messageId in candidateIds)
        {
            // Optimistic claim: only one worker wins Pending → Processing.
            var claimed = await dbContext.OutboxMessages
                .Where(m => m.Id == messageId && m.Status == OutboxMessageStatus.Pending)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(m => m.Status, OutboxMessageStatus.Processing)
                        .SetProperty(m => m.ProcessingStartedAt, now)
                        .SetProperty(m => m.Error, (string?)null),
                    ct);

            if (claimed == 0)
            {
                _logger.LogDebug("Outbox message {Id} already claimed by another worker", messageId);
                continue;
            }

            var tracked = await dbContext.OutboxMessages
                .FirstAsync(m => m.Id == messageId, ct);

            await ProcessSingleMessageAsync(dbContext, dispatcher, tracked, ct);
            handled++;
        }

        return handled;
    }

    private async Task ProcessSingleMessageAsync(
        HealthcareDbContext dbContext,
        IDomainEventDispatcher dispatcher,
        OutboxMessage message,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var eventTypeName = message.EventType;

        try
        {
            var eventType = Type.GetType(message.EventType);
            if (eventType is null)
            {
                message.MarkFailed(
                    new InvalidOperationException($"Unknown event type: {message.EventType}"),
                    _settings.MaxRetryAttempts,
                    _settings.BaseRetryDelay,
                    _settings.MaxRetryDelay,
                    nonRetryable: true);

                await dbContext.SaveChangesAsync(ct);
                _metrics.RecordDeadLettered(eventTypeName, "unknown_type");
                _logger.LogError(
                    "Outbox message {Id} MessageId={MessageId} dead-lettered: cannot resolve type '{EventType}'",
                    message.Id, message.MessageId, message.EventType);
                return;
            }

            eventTypeName = eventType.Name;

            var domainEvent = JsonSerializer.Deserialize(message.Payload, eventType) as IDomainEvent
                ?? throw new InvalidOperationException(
                    $"Payload for {eventType.Name} did not deserialize to IDomainEvent.");

            // Idempotent at envelope level: MessageId unique + Processed is terminal.
            // Strict dispatch: any handler failure → retry / dead-letter.
            await dispatcher.DispatchStrictAsync(new[] { domainEvent }, ct);

            message.MarkProcessed(DateTime.UtcNow);
            await dbContext.SaveChangesAsync(ct);

            sw.Stop();
            _metrics.RecordProcessed(eventTypeName, sw.Elapsed.TotalMilliseconds);

            _logger.LogInformation(
                "Outbox message {Id} MessageId={MessageId} ({EventType}) processed in {ElapsedMs}ms",
                message.Id, message.MessageId, eventTypeName, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var nonRetryable = ex is JsonException || ex is NotSupportedException;

            message.MarkFailed(
                ex,
                _settings.MaxRetryAttempts,
                _settings.BaseRetryDelay,
                _settings.MaxRetryDelay,
                nonRetryable);

            await dbContext.SaveChangesAsync(ct);

            if (message.Status == OutboxMessageStatus.DeadLetter)
            {
                _metrics.RecordDeadLettered(eventTypeName, nonRetryable ? "non_retryable" : "max_retries");
                _logger.LogError(
                    ex,
                    "Outbox message {Id} MessageId={MessageId} dead-lettered after {RetryCount} attempt(s)",
                    message.Id, message.MessageId, message.RetryCount);
            }
            else
            {
                _metrics.RecordFailed(eventTypeName, sw.Elapsed.TotalMilliseconds);
                _logger.LogWarning(
                    ex,
                    "Outbox message {Id} MessageId={MessageId} failed (attempt {RetryCount}/{Max}); next at {NextAttempt:u}",
                    message.Id,
                    message.MessageId,
                    message.RetryCount,
                    _settings.MaxRetryAttempts,
                    message.NextAttemptAt);
            }
        }
    }

    private async Task ReleaseStaleClaimsAsync(
        HealthcareDbContext dbContext,
        DateTime utcNow,
        CancellationToken ct)
    {
        var leaseCutoff = utcNow - _settings.ProcessingLease;
        var stale = await dbContext.OutboxMessages
            .Where(m =>
                m.Status == OutboxMessageStatus.Processing &&
                m.ProcessingStartedAt != null &&
                m.ProcessingStartedAt < leaseCutoff)
            .ToListAsync(ct);

        if (stale.Count == 0)
            return;

        foreach (var message in stale)
            message.ReleaseStaleClaim(utcNow, _settings.ProcessingLease);

        await dbContext.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Released {Count} stale outbox Processing claim(s) older than {Lease}s",
            stale.Count,
            _settings.ProcessingLease.TotalSeconds);
    }
}
