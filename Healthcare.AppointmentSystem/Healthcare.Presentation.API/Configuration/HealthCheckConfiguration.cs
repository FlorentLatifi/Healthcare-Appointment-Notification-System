using System.Text.Json;
using Healthcare.Presentation.API.Authorization;
using Healthcare.Presentation.API.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Healthcare.Presentation.API.Configuration;

public static class HealthCheckConfiguration
{
    public static IHealthChecksBuilder AddHealthcareHealthChecks(this IHealthChecksBuilder builder)
    {
        return builder
            .AddCheck<SelfHealthCheck>(
                "self",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "live", "self" })
            .AddCheck<DatabaseHealthCheck>(
                "database",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "ready", "db", "sql", "critical" })
            .AddCheck<RedisHealthCheck>(
                "redis",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "ready", "cache", "redis", "locking" })
            .AddCheck<MemoryHealthCheck>(
                "memory",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "ready", "memory", "performance" })
            .AddCheck<OutboxRelayHealthCheck>(
                "outbox-relay",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "ready", "background", "outbox" })
            .AddCheck<AppointmentReminderHealthCheck>(
                "appointment-reminder",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "ready", "background", "reminders" });
    }

    public static WebApplication MapHealthcareHealthEndpoints(this WebApplication app)
    {
        // Liveness: process only (k8s/docker restart decision)
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live"),
            ResponseWriter = WriteMinimalAsync
        });

        // Readiness: critical dependencies (traffic admission)
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("ready") || r.Tags.Contains("critical"),
            ResponseWriter = WriteStatusWithSummaryAsync
        });

        // Backward-compatible aggregate (status only, anonymous)
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => true,
            ResponseWriter = WriteMinimalAsync
        });

        // Detailed diagnostics — Admin only
        app.MapHealthChecks("/health/details", new HealthCheckOptions
        {
            Predicate = _ => true,
            ResponseWriter = WriteDetailedAsync
        }).RequireAuthorization(policy => policy.RequireRole(AppRoles.Admin));

        return app;
    }

    private static Task WriteMinimalAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var payload = JsonSerializer.Serialize(new { status = report.Status.ToString() });
        return context.Response.WriteAsync(payload);
    }

    private static Task WriteStatusWithSummaryAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var payload = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.ToDictionary(
                e => e.Key,
                e => e.Value.Status.ToString())
        });
        return context.Response.WriteAsync(payload);
    }

    private static Task WriteDetailedAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var payload = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = e.Value.Duration.TotalMilliseconds,
                tags = e.Value.Tags,
                data = e.Value.Data,
                error = e.Value.Exception?.Message
            })
        }, new JsonSerializerOptions { WriteIndented = true });
        return context.Response.WriteAsync(payload);
    }
}
