using Healthcare.Adapters.Persistence.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Healthcare.Presentation.API.HealthChecks;

/// <summary>
/// Health check for database connectivity only — no business-data counts.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly HealthcareDbContext _context;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(
        HealthcareDbContext context,
        ILogger<DatabaseHealthCheck> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);

            if (!canConnect)
            {
                _logger.LogWarning("Database health check failed: Cannot connect");
                return HealthCheckResult.Unhealthy("Cannot connect to database");
            }

            // Lightweight round-trip without reading application tables / row counts.
            await _context.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);

            return HealthCheckResult.Healthy("Database is reachable.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed with exception");

            return HealthCheckResult.Unhealthy(
                "Database health check failed",
                ex);
        }
    }
}
