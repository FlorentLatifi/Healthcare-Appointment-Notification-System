using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Healthcare.Presentation.API.Configuration;

public static class ObservabilityConfiguration
{
    public static IServiceCollection AddObservability(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("HealthcareAPI"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddConsoleExporter());

        return services;
    }
}
