using Healthcare.Adapters.Background;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Healthcare.Presentation.API.HealthChecks;

/// <summary>
/// Generic health check for background workers driven by <see cref="WorkerHealthState"/>.
/// </summary>
public sealed class WorkerHealthCheck : IHealthCheck
{
    private readonly WorkerHealthState _state;
    private readonly Func<TimeSpan> _staleAfterFactory;
    private readonly string _displayName;

    public WorkerHealthCheck(
        WorkerHealthState state,
        string displayName,
        Func<TimeSpan> staleAfterFactory)
    {
        _state = state;
        _displayName = displayName;
        _staleAfterFactory = staleAfterFactory;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var snap = _state.Snapshot();
        var data = new Dictionary<string, object>
        {
            ["worker"] = snap.WorkerName,
            ["enabled"] = snap.IsEnabled,
            ["running"] = snap.IsRunning,
            ["stopping"] = snap.IsStopping,
            ["circuitOpen"] = snap.IsCircuitOpen,
            ["lastAttemptUtc"] = snap.LastAttemptUtc?.ToString("O") ?? "never",
            ["lastSuccessUtc"] = snap.LastSuccessUtc?.ToString("O") ?? "never",
            ["lastFailureUtc"] = snap.LastFailureUtc?.ToString("O") ?? "never",
            ["lastError"] = snap.LastError ?? string.Empty,
            ["successfulBatches"] = snap.SuccessfulBatches,
            ["failedBatches"] = snap.FailedBatches,
            ["consecutiveFailures"] = snap.ConsecutiveFailures
        };

        if (!snap.IsEnabled)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                description: $"{_displayName} is disabled by configuration.",
                data: data));
        }

        if (snap.IsCircuitOpen)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                description: $"{_displayName} circuit breaker is OPEN.",
                data: data));
        }

        if (snap.IsStopping)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                description: $"{_displayName} is shutting down gracefully.",
                data: data));
        }

        if (snap.IsRunning && snap.ConsecutiveFailures >= 3)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                description: $"{_displayName} has {snap.ConsecutiveFailures} consecutive batch failures.",
                data: data));
        }

        var staleAfter = _staleAfterFactory();
        if (staleAfter > TimeSpan.Zero
            && snap.IsRunning
            && snap.LastSuccessUtc is { } lastSuccess
            && DateTime.UtcNow - lastSuccess > staleAfter)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                description: $"{_displayName} has not completed a successful batch within {staleAfter.TotalMinutes:0} minutes.",
                data: data));
        }

        // Just started — no success yet is OK for a short grace period.
        if (snap.IsRunning && snap.LastSuccessUtc is null && snap.StartedAtUtc is { } started
            && DateTime.UtcNow - started < TimeSpan.FromMinutes(5))
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                description: $"{_displayName} is starting (no completed batch yet).",
                data: data));
        }

        if (snap.IsRunning && snap.LastSuccessUtc is null && staleAfter > TimeSpan.Zero
            && snap.StartedAtUtc is { } startedAt
            && DateTime.UtcNow - startedAt > staleAfter)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                description: $"{_displayName} never completed a successful batch since start.",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            description: $"{_displayName} is healthy.",
            data: data));
    }
}
