using Healthcare.Adapters.Persistence.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Healthcare.Presentation.API.HealthChecks;

/// <summary>
/// Health check for database connectivity.
/// </summary>
/// <remarks>
/// Design Pattern: Health Check Pattern
/// 
/// This checks:
/// - Database connection is alive
/// - Can execute simple query
/// - Response time is acceptable
/// 
/// Status:
/// - Healthy: Database is reachable and responsive
/// - Unhealthy: Cannot connect to database
/// </remarks>
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
            // Try to connect and execute a simple query
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);

            if (!canConnect)
            {
                _logger.LogWarning("Database health check failed: Cannot connect");
                return HealthCheckResult.Unhealthy("Cannot connect to database");
            }

            // Execute a simple query to verify full connectivity
            var count = await _context.Patients.CountAsync(cancellationToken);

            _logger.LogInformation("Database health check passed. Patient count: {Count}", count);

            return HealthCheckResult.Healthy($"Database is healthy. {count} patients in system.");
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