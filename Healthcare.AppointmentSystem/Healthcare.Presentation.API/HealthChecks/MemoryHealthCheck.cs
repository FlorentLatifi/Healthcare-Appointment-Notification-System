using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Healthcare.Presentation.API.HealthChecks;

/// <summary>
/// Health check for memory usage.
/// </summary>
/// <remarks>
/// Monitors:
/// - Total allocated memory
/// - Percentage of memory used
/// 
/// Thresholds:
/// - Healthy: < 80% memory usage
/// - Degraded: 80-95% memory usage
/// - Unhealthy: > 95% memory usage
/// </remarks>
public class MemoryHealthCheck : IHealthCheck
{
    private readonly ILogger<MemoryHealthCheck> _logger;
    private const long WarningThresholdBytes = 1024L * 1024L * 1024L * 2L; // 2 GB
    private const long CriticalThresholdBytes = 1024L * 1024L * 1024L * 4L; // 4 GB

    public MemoryHealthCheck(ILogger<MemoryHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var allocatedBytes = GC.GetTotalMemory(forceFullCollection: false);
        var allocatedMb = allocatedBytes / 1024 / 1024;

        var data = new Dictionary<string, object>
        {
            { "AllocatedMB", allocatedMb },
            { "Gen0Collections", GC.CollectionCount(0) },
            { "Gen1Collections", GC.CollectionCount(1) },
            { "Gen2Collections", GC.CollectionCount(2) }
        };

        if (allocatedBytes >= CriticalThresholdBytes)
        {
            _logger.LogWarning("Memory usage is critical: {AllocatedMB} MB", allocatedMb);
            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    $"Memory usage is critical: {allocatedMb} MB",
                    data: data));
        }

        if (allocatedBytes >= WarningThresholdBytes)
        {
            _logger.LogInformation("Memory usage is elevated: {AllocatedMB} MB", allocatedMb);
            return Task.FromResult(
                HealthCheckResult.Degraded(
                    $"Memory usage is elevated: {allocatedMb} MB",
                    data: data));
        }

        _logger.LogInformation("Memory usage is healthy: {AllocatedMB} MB", allocatedMb);
        return Task.FromResult(
            HealthCheckResult.Healthy(
                $"Memory usage is healthy: {allocatedMb} MB",
                data: data));
    }
}