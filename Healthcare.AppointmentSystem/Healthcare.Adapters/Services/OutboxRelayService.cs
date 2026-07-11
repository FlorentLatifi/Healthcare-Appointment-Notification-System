using System.Diagnostics;
using System.Text.Json;
using Healthcare.Adapters.Background;
using Healthcare.Adapters.Events;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace Healthcare.Adapters.Services;

/// <summary>
/// Production outbox relay: claim → strict dispatch → processed / retry / dead-letter,
/// with Polly retry + circuit breaker, graceful shutdown, metrics, and health/alert hooks.
/// </summary>
public sealed class OutboxRelayService : BackgroundService
{
    public const string WorkerName = OutboxRelayHealthState.Name;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxRelayService> _logger;
    private readonly OutboxSettings _settings;
    private readonly OutboxMetrics _metrics;
    private readonly OutboxRelayHealthState _health;
    private readonly IBackgroundWorkerAlert _alerts;
    private readonly ResiliencePipeline _batchPipeline;

    public OutboxRelayService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxRelayService> logger,
        OutboxSettings settings,
        OutboxMetrics metrics,
        OutboxRelayHealthState health,
        IBackgroundWorkerAlert alerts)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = settings;
        _metrics = metrics;
        _health = health;
        _alerts = alerts;

        _batchPipeline = BackgroundResiliencePipelineFactory.Create(
            WorkerName,
            settings.BatchPollyRetryAttempts,
            settings.BatchPollyRetryBaseDelay,
            settings.CircuitBreakerFailureThreshold,
            settings.CircuitBreakDuration,
            health,
            alerts,
            logger);
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _health.MarkEnabled(_settings.UseOutboxForDomainEvents);
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{Worker} graceful shutdown requested (timeout={Timeout}s)",
            WorkerName, _settings.ShutdownTimeout.TotalSeconds);
        _health.MarkStopping();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_settings.ShutdownTimeout);

        try
        {
            await base.StopAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("{Worker} shutdown timed out after {Timeout}s; forcing stop",
                WorkerName, _settings.ShutdownTimeout.TotalSeconds);
            await _alerts.NotifyAsync(new BackgroundWorkerAlert(
                WorkerName,
                BackgroundWorkerAlertSeverity.Warning,
                "shutdown_timeout",
                "Outbox relay did not finish in-flight work before shutdown timeout."), cancellationToken);
        }
        finally
        {
            _health.MarkStopped();
            _logger.LogInformation("{Worker} stopped", WorkerName);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.UseOutboxForDomainEvents)
        {
            _health.MarkEnabled(false);
            _logger.LogInformation("{Worker} is disabled (UseOutboxForDomainEvents is false).", WorkerName);
            return;
        }

        _health.MarkStarted();
        _logger.LogInformation(
            "{Worker} started (interval={Interval}s, maxMsgRetries={MaxRetries}, batch={Batch}, " +
            "pollyRetries={PollyRetries}, circuit={CircuitThreshold}/{CircuitBreak}s, lease={Lease}s)",
            WorkerName,
            _settings.RelayIntervalSeconds,
            _settings.MaxRetryAttempts,
            _settings.BatchSize,
            _settings.BatchPollyRetryAttempts,
            _settings.CircuitBreakerFailureThreshold,
            _settings.CircuitBreakerBreakSeconds,
            _settings.ProcessingLeaseSeconds);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnceSafeAsync(stoppingToken);

                if (stoppingToken.IsCancellationRequested)
                    break;

                var delay = _health.Snapshot().IsCircuitOpen
                    ? _settings.CircuitBreakDuration
                    : TimeSpan.FromSeconds(_settings.RelayIntervalSeconds);

                if (!await DelaySafeAsync(delay, stoppingToken))
                    break;
            }
        }
        finally
        {
            _health.MarkStopped();
            _logger.LogInformation("{Worker} execute loop exited", WorkerName);
        }
    }

    private async Task RunOnceSafeAsync(CancellationToken stoppingToken)
    {
        _health.MarkAttempt();
        var sw = Stopwatch.StartNew();

        try
        {
            var processedCount = await _batchPipeline.ExecuteAsync(
                static async (state, ct) => await state.ProcessBatchAsync(ct),
                this,
                stoppingToken);

            sw.Stop();
            _metrics.RecordBatch(processedCount, sw.Elapsed.TotalMilliseconds, success: true);
            _health.MarkSuccess();

            _logger.LogDebug(
                "{Worker} batch ok in {ElapsedMs}ms (handled={Count})",
                WorkerName, sw.ElapsedMilliseconds, processedCount);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("{Worker} batch cancelled (host stopping)", WorkerName);
        }
        catch (BrokenCircuitException ex)
        {
            sw.Stop();
            _metrics.RecordBatch(0, sw.Elapsed.TotalMilliseconds, success: false);
            _metrics.RecordCircuitOpen();
            _health.MarkFailure(ex);

            _logger.LogWarning(
                "{Worker} skipped batch — circuit is open",
                WorkerName);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _metrics.RecordBatch(0, sw.Elapsed.TotalMilliseconds, success: false);
            _health.MarkFailure(ex);

            _logger.LogError(ex, "{Worker} batch failed after Polly retries", WorkerName);

            await SafeAlertAsync(new BackgroundWorkerAlert(
                WorkerName,
                BackgroundWorkerAlertSeverity.Error,
                "batch_failed",
                "Outbox relay batch failed after retries.",
                ex,
                new Dictionary<string, object?>
                {
                    ["ElapsedMs"] = sw.ElapsedMilliseconds,
                    ["ConsecutiveFailures"] = _health.Snapshot().ConsecutiveFailures
                }), stoppingToken);
        }
    }

    /// <summary>Processes one batch. Internal for unit tests.</summary>
    internal async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HealthcareDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();

        var now = DateTime.UtcNow;

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
            "{Worker} claiming up to {Count} due message(s) (batchSize={BatchSize})",
            WorkerName, candidateIds.Count, _settings.BatchSize);

        var handled = 0;

        foreach (var messageId in candidateIds)
        {
            ct.ThrowIfCancellationRequested();

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
                _logger.LogDebug("{Worker} message {Id} already claimed", WorkerName, messageId);
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
                    "{Worker} message {Id} MessageId={MessageId} dead-lettered: unknown type '{EventType}'",
                    WorkerName, message.Id, message.MessageId, message.EventType);

                await SafeAlertAsync(new BackgroundWorkerAlert(
                    WorkerName,
                    BackgroundWorkerAlertSeverity.Error,
                    "dead_letter_unknown_type",
                    $"Outbox message {message.MessageId} dead-lettered (unknown type).",
                    Data: new Dictionary<string, object?>
                    {
                        ["MessageId"] = message.MessageId,
                        ["EventType"] = message.EventType
                    }), ct);
                return;
            }

            eventTypeName = eventType.Name;

            var domainEvent = JsonSerializer.Deserialize(message.Payload, eventType) as IDomainEvent
                ?? throw new InvalidOperationException(
                    $"Payload for {eventType.Name} did not deserialize to IDomainEvent.");

            await dispatcher.DispatchStrictAsync(new[] { domainEvent }, ct);

            message.MarkProcessed(DateTime.UtcNow);
            await dbContext.SaveChangesAsync(ct);

            sw.Stop();
            _metrics.RecordProcessed(eventTypeName, sw.Elapsed.TotalMilliseconds);

            _logger.LogInformation(
                "{Worker} message {Id} MessageId={MessageId} ({EventType}) processed in {ElapsedMs}ms",
                WorkerName, message.Id, message.MessageId, eventTypeName, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Best-effort release so lease recovery is not required after restart.
            try
            {
                await dbContext.OutboxMessages
                    .Where(m => m.Id == message.Id && m.Status == OutboxMessageStatus.Processing)
                    .ExecuteUpdateAsync(
                        s => s
                            .SetProperty(m => m.Status, OutboxMessageStatus.Pending)
                            .SetProperty(m => m.ProcessingStartedAt, (DateTime?)null)
                            .SetProperty(m => m.NextAttemptAt, DateTime.UtcNow)
                            .SetProperty(m => m.Error, "Released due to cancellation during processing."),
                        CancellationToken.None);
            }
            catch (Exception releaseEx)
            {
                _logger.LogWarning(releaseEx,
                    "{Worker} failed to release claim for message {Id} on cancel",
                    WorkerName, message.Id);
            }

            throw;
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

            // Prefer completing persistence even if host is stopping.
            var saveCt = ct.IsCancellationRequested ? CancellationToken.None : ct;
            await dbContext.SaveChangesAsync(saveCt);

            if (message.Status == OutboxMessageStatus.DeadLetter)
            {
                _metrics.RecordDeadLettered(eventTypeName, nonRetryable ? "non_retryable" : "max_retries");
                _logger.LogError(
                    ex,
                    "{Worker} message {Id} MessageId={MessageId} dead-lettered after {RetryCount} attempt(s)",
                    WorkerName, message.Id, message.MessageId, message.RetryCount);

                await SafeAlertAsync(new BackgroundWorkerAlert(
                    WorkerName,
                    BackgroundWorkerAlertSeverity.Error,
                    "dead_letter",
                    $"Outbox message {message.MessageId} moved to dead-letter.",
                    ex,
                    new Dictionary<string, object?>
                    {
                        ["MessageId"] = message.MessageId,
                        ["EventType"] = eventTypeName,
                        ["RetryCount"] = message.RetryCount,
                        ["NonRetryable"] = nonRetryable
                    }), saveCt);
            }
            else
            {
                _metrics.RecordFailed(eventTypeName, sw.Elapsed.TotalMilliseconds);
                _logger.LogWarning(
                    ex,
                    "{Worker} message {Id} MessageId={MessageId} failed (attempt {RetryCount}/{Max}); next at {NextAttempt:u}",
                    WorkerName,
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
            "{Worker} released {Count} stale Processing claim(s) older than {Lease}s",
            WorkerName, stale.Count, _settings.ProcessingLease.TotalSeconds);

        await SafeAlertAsync(new BackgroundWorkerAlert(
            WorkerName,
            BackgroundWorkerAlertSeverity.Warning,
            "stale_claims_released",
            $"Released {stale.Count} stale outbox processing claim(s).",
            Data: new Dictionary<string, object?> { ["Count"] = stale.Count }), ct);
    }

    private static async Task<bool> DelaySafeAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
    }

    private async Task SafeAlertAsync(BackgroundWorkerAlert alert, CancellationToken ct)
    {
        try
        {
            await _alerts.NotifyAsync(alert, ct.IsCancellationRequested ? CancellationToken.None : ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "{Worker} alert sink failed for {Code}", WorkerName, alert.Code);
        }
    }
}
