using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Healthcare.Adapters.Background;

/// <summary>
/// Builds Polly v8 pipelines: exponential retry (with jitter) + circuit breaker for batch workers.
/// </summary>
public static class BackgroundResiliencePipelineFactory
{
    public static ResiliencePipeline Create(
        string workerName,
        int maxRetryAttempts,
        TimeSpan baseRetryDelay,
        int circuitFailureThreshold,
        TimeSpan circuitBreakDuration,
        WorkerHealthState health,
        IBackgroundWorkerAlert alerts,
        ILogger logger)
    {
        maxRetryAttempts = Math.Max(0, maxRetryAttempts);
        circuitFailureThreshold = Math.Max(2, circuitFailureThreshold);
        if (baseRetryDelay <= TimeSpan.Zero)
            baseRetryDelay = TimeSpan.FromSeconds(1);
        if (circuitBreakDuration <= TimeSpan.Zero)
            circuitBreakDuration = TimeSpan.FromSeconds(30);

        // Sampling window must exceed break considerations; keep simple and stable.
        var samplingDuration = TimeSpan.FromSeconds(
            Math.Max(30, circuitBreakDuration.TotalSeconds));

        var builder = new ResiliencePipelineBuilder();

        if (maxRetryAttempts > 0)
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetryAttempts,
                Delay = baseRetryDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(IsTransientBatchFailure),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "{Worker} batch retry {Attempt}/{Max} after {DelayMs}ms",
                        workerName,
                        args.AttemptNumber,
                        maxRetryAttempts,
                        args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            });
        }

        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            // Count every handled failure; open after threshold throughput of failures.
            FailureRatio = 1.0,
            MinimumThroughput = circuitFailureThreshold,
            SamplingDuration = samplingDuration,
            BreakDuration = circuitBreakDuration,
            ShouldHandle = new PredicateBuilder()
                .Handle<Exception>(IsTransientBatchFailure),
            OnOpened = args =>
            {
                health.MarkCircuitOpen();
                logger.LogError(
                    args.Outcome.Exception,
                    "{Worker} circuit OPEN for {BreakSeconds}s",
                    workerName,
                    circuitBreakDuration.TotalSeconds);

                // Fire-and-forget alert; do not block pipeline callbacks.
                _ = SafeAlertAsync(alerts, new BackgroundWorkerAlert(
                    workerName,
                    BackgroundWorkerAlertSeverity.Critical,
                    "circuit_open",
                    $"Circuit breaker opened for {workerName}.",
                    args.Outcome.Exception,
                    new Dictionary<string, object?>
                    {
                        ["BreakSeconds"] = circuitBreakDuration.TotalSeconds,
                        ["FailureRatio"] = 1.0,
                        ["MinimumThroughput"] = circuitFailureThreshold
                    }));

                return default;
            },
            OnClosed = _ =>
            {
                health.MarkCircuitClosed();
                logger.LogInformation("{Worker} circuit CLOSED", workerName);
                return default;
            },
            OnHalfOpened = _ =>
            {
                logger.LogWarning("{Worker} circuit HALF-OPEN (probe allowed)", workerName);
                return default;
            }
        });

        return builder.Build();
    }

    /// <summary>
    /// Batch-level failures that should trip retries / the circuit.
    /// Cancellation and broken-circuit itself are not "retryable application failures".
    /// </summary>
    public static bool IsTransientBatchFailure(Exception exception) =>
        exception is not OperationCanceledException
        && exception is not TaskCanceledException
        && exception is not BrokenCircuitException;

    private static async Task SafeAlertAsync(IBackgroundWorkerAlert alerts, BackgroundWorkerAlert alert)
    {
        try
        {
            await alerts.NotifyAsync(alert);
        }
        catch
        {
            // Never let alerting break the worker.
        }
    }
}
