using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Healthcare.Presentation.API.HealthChecks;

/// <summary>
/// Database connectivity + latency health check (critical for readiness).
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(IConfiguration configuration, ILogger<DatabaseHealthCheck> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = 3;
            await command.ExecuteScalarAsync(cancellationToken);
            sw.Stop();

            var data = new Dictionary<string, object>
            {
                ["latencyMs"] = sw.Elapsed.TotalMilliseconds,
                ["serverVersion"] = connection.ServerVersion
            };

            if (sw.ElapsedMilliseconds > 1000)
            {
                return HealthCheckResult.Degraded(
                    $"Database reachable but slow ({sw.ElapsedMilliseconds}ms).",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"Database is reachable ({sw.ElapsedMilliseconds}ms).",
                data);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Database health check failed after {ElapsedMs}ms", sw.ElapsedMilliseconds);
            return HealthCheckResult.Unhealthy(
                "Database health check failed",
                ex,
                new Dictionary<string, object> { ["latencyMs"] = sw.Elapsed.TotalMilliseconds });
        }
    }
}
