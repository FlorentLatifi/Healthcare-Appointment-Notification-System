using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Healthcare.Presentation.API.Configuration;

public static class ObservabilityConfiguration
{
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var otelEndpoint = configuration.GetValue<string>("Otel:Endpoint");
        var useOtlp = !string.IsNullOrWhiteSpace(otelEndpoint);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("HealthcareAPI"))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();

                if (useOtlp)
                    tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otelEndpoint!));
                else
                    tracing.AddConsoleExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    // Transactional outbox relay metrics (Meter: Healthcare.Outbox)
                    .AddMeter(Healthcare.Adapters.Events.OutboxMetrics.MeterName);

                if (useOtlp)
                    metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otelEndpoint!));
                else
                    metrics.AddConsoleExporter();
            });

        return services;
    }
}
