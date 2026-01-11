using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Healthcare.Presentation.API.HealthChecks;

/// <summary>
/// Health check for Redis connectivity.
/// </summary>
public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisHealthCheck> _logger;

    public RedisHealthCheck(
        IConnectionMultiplexer redis,
        ILogger<RedisHealthCheck> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();

            

            // Perform a PING command
            var pingTime = await db.PingAsync();

            var data = new Dictionary<string, object>
            {
                { "PingTime", $"{pingTime.TotalMilliseconds:F2}ms" },
                { "IsConnected", _redis.IsConnected },
                { "Endpoints", string.Join(", ", _redis.GetEndPoints().Select(e => e.ToString())) }
            };

            if (!_redis.IsConnected)
            {
                _logger.LogWarning("Redis health check failed: Not connected");
                return HealthCheckResult.Unhealthy("Redis is not connected", data: data);
            }

            if (pingTime.TotalMilliseconds > 100)
            {
                _logger.LogWarning("Redis health check degraded: High latency ({PingTime}ms)",
                    pingTime.TotalMilliseconds);
                return HealthCheckResult.Degraded(
                    $"Redis responding slowly ({pingTime.TotalMilliseconds:F2}ms)",
                    data: data);
            }

            _logger.LogInformation("Redis health check passed: {PingTime}ms",
                pingTime.TotalMilliseconds);

            return HealthCheckResult.Healthy(
                $"Redis is healthy (ping: {pingTime.TotalMilliseconds:F2}ms)",
                data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis health check failed with exception");
            return HealthCheckResult.Unhealthy("Redis health check failed", ex);
        }
    }
}