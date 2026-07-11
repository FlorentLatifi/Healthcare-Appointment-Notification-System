using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.OpenTelemetry;

namespace Healthcare.Presentation.API.Configuration;

public static class SerilogConfiguration
{
    public static void ConfigureBootstrapLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(new RenderedCompactJsonFormatter())
            .CreateBootstrapLogger();
    }

    public static LoggerConfiguration ConfigureSerilog(
        this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var otelEndpoint = configuration["Otel:Endpoint"];
        var serviceName = configuration["Otel:ServiceName"] ?? ObservabilityConfiguration.ServiceName;

        loggerConfiguration
            .ReadFrom.Configuration(configuration)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("Application", serviceName)
            .Enrich.WithProperty("Environment", environment.EnvironmentName)
            .WriteTo.Console(new RenderedCompactJsonFormatter())
            .WriteTo.File(
                new CompactJsonFormatter(),
                path: "logs/healthcare-api-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true);

        if (!string.IsNullOrWhiteSpace(otelEndpoint))
        {
            loggerConfiguration.WriteTo.OpenTelemetry(options =>
            {
                options.Endpoint = otelEndpoint;
                options.Protocol = OtlpProtocol.Grpc;
                options.ResourceAttributes = new Dictionary<string, object>
                {
                    ["service.name"] = serviceName,
                    ["deployment.environment"] = environment.EnvironmentName
                };
                options.IncludedData =
                    IncludedData.SpanIdField |
                    IncludedData.TraceIdField |
                    IncludedData.MessageTemplateTextAttribute;
            });
        }

        return loggerConfiguration;
    }
}
