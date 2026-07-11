using Healthcare.Adapters.Background;
using Healthcare.Adapters.Persistence.EntityFramework;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Healthcare.Presentation.API.HealthChecks;

public sealed class OutboxRelayHealthCheck : IHealthCheck
{
    private readonly WorkerHealthCheck _inner;

    public OutboxRelayHealthCheck(OutboxRelayHealthState state, OutboxSettings settings)
    {
        _inner = new WorkerHealthCheck(
            state,
            "Outbox relay",
            () => settings.UnhealthyIfNoSuccessMinutes <= 0
                ? TimeSpan.Zero
                : TimeSpan.FromMinutes(settings.UnhealthyIfNoSuccessMinutes));
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        _inner.CheckHealthAsync(context, cancellationToken);
}
