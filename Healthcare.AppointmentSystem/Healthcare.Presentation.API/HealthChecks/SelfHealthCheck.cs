using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Healthcare.Presentation.API.HealthChecks;

/// <summary>
/// Process liveness — always Healthy if the host is running.
/// </summary>
public sealed class SelfHealthCheck : IHealthCheck
{
    private static readonly DateTime StartedUtc = DateTime.UtcNow;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var proc = Process.GetCurrentProcess();
        var data = new Dictionary<string, object>
        {
            ["uptimeSeconds"] = (DateTime.UtcNow - StartedUtc).TotalSeconds,
            ["workingSetMb"] = proc.WorkingSet64 / (1024.0 * 1024.0),
            ["threadCount"] = proc.Threads.Count,
            ["environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "unknown"
        };

        return Task.FromResult(HealthCheckResult.Healthy("Process is running.", data));
    }
}
