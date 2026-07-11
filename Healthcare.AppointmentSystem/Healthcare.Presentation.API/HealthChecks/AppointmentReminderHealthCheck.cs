using Healthcare.Adapters.Background;
using Healthcare.Presentation.API.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Healthcare.Presentation.API.HealthChecks;

public sealed class AppointmentReminderHealthCheck : IHealthCheck
{
    private readonly WorkerHealthCheck _inner;

    public AppointmentReminderHealthCheck(
        AppointmentReminderHealthState state,
        IOptions<ReminderSettings> settings)
    {
        var s = settings.Value;
        _inner = new WorkerHealthCheck(
            state,
            "Appointment reminder",
            () => s.UnhealthyIfNoSuccessMinutes <= 0
                ? TimeSpan.Zero
                : TimeSpan.FromMinutes(s.UnhealthyIfNoSuccessMinutes));
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        _inner.CheckHealthAsync(context, cancellationToken);
}
