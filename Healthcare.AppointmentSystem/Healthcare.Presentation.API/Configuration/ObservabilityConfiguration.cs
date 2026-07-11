using Healthcare.Application.Observability;
using Healthcare.Presentation.API.Http;
using Microsoft.Extensions.Http;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Healthcare.Presentation.API.Configuration;

public static class ObservabilityConfiguration
{
    public const string ServiceName = "HealthcareAPI";

    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var otel = configuration.GetSection("Otel");
        var endpoint = otel["Endpoint"];
        var useOtlp = !string.IsNullOrWhiteSpace(endpoint);
        var serviceVersion = typeof(ObservabilityConfiguration).Assembly.GetName().Version?.ToString() ?? "1.0.0";

        // Outbound HTTP correlation propagation (all IHttpClientFactory clients)
        services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HttpMessageHandlerBuilderActions.Add(builder =>
            {
                // Stateless handler — construct directly to avoid scope issues in test hosts
                builder.AdditionalHandlers.Add(new CorrelationIdDelegatingHandler());
            });
        });

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: otel["ServiceName"] ?? ServiceName,
                    serviceVersion: serviceVersion,
                    serviceInstanceId: Environment.MachineName)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = environment.EnvironmentName,
                    ["service.namespace"] = "healthcare"
                }))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(HealthcareActivitySource.Name)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = ctx =>
                            !ctx.Request.Path.StartsWithSegments("/health");
                        options.EnrichWithHttpRequest = (activity, request) =>
                        {
                            if (request.HttpContext.Items.TryGetValue(CorrelationContext.HttpContextItemKey, out var cid)
                                && cid is string correlationId)
                            {
                                activity.SetTag(CorrelationContext.TagName, correlationId);
                                activity.SetBaggage(CorrelationContext.BaggageKey, correlationId);
                            }
                        };
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddEntityFrameworkCoreInstrumentation();

                if (useOtlp)
                {
                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(endpoint!);
                        var protocol = otel["Protocol"];
                        if (string.Equals(protocol, "http/protobuf", StringComparison.OrdinalIgnoreCase))
                            options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    });
                }
                else if (environment.IsDevelopment())
                {
                    tracing.AddConsoleExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddMeter(BusinessMetrics.MeterName)
                    .AddMeter(Healthcare.Adapters.Events.OutboxMetrics.MeterName)
                    .AddMeter(Healthcare.Presentation.API.Services.ReminderMetrics.MeterName);

                if (useOtlp)
                {
                    metrics.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(endpoint!);
                        var protocol = otel["Protocol"];
                        if (string.Equals(protocol, "http/protobuf", StringComparison.OrdinalIgnoreCase))
                            options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    });
                }
                else if (environment.IsDevelopment())
                {
                    metrics.AddConsoleExporter();
                }
            });

        return services;
    }
}
