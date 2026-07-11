using Healthcare.Adapters;
using Healthcare.Adapters.Events;
using Healthcare.Adapters.Services;
using Healthcare.Application.Commands.AnonymizePatient;
using Healthcare.Application.Commands.BookAppointment;
using Healthcare.Application.Commands.CancelAppointment;
using Healthcare.Application.Commands.CompleteAppointment;
using Healthcare.Application.Commands.ConfirmAppointment;
using Healthcare.Application.Commands.CreateDoctor;
using Healthcare.Application.Commands.CreatePatient;
using Healthcare.Application.Commands.DeactivateDoctor;
using Healthcare.Application.Commands.ForgotPassword;
using Healthcare.Application.Commands.MarkNoShowAppointment;
using Healthcare.Application.Commands.ProcessPayment;
using Healthcare.Application.Commands.RefundPayment;
using Healthcare.Application.Commands.ResetPassword;
using Healthcare.Application.Common;
using Healthcare.Application.DTOs;

using Healthcare.Application.Ports.Payments;
using Healthcare.Application.Queries.Analytics;
using Healthcare.Application.Queries.GetAppointment;

using Healthcare.Application;
using Healthcare.Application.Services;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Presentation.API.Authorization;
using Healthcare.Presentation.API.Configuration;
using Healthcare.Presentation.API.Middleware;
using Healthcare.Presentation.API.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {CorrelationId}{NewLine}{Exception}")
    .WriteTo.File("logs/healthcare-api-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting Healthcare API...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    // Limit request body size (DoS / oversized payload protection). 1 MiB is enough for JSON APIs.
    const long maxRequestBodyBytes = 1_048_576;
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = maxRequestBodyBytes;
    });
    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = maxRequestBodyBytes;
        options.ValueLengthLimit = 256 * 1024;
    });

    // ── API Infrastructure ──────────────────────────────────
    builder.Services.AddApiInfrastructure();

    // ── Security ────────────────────────────────────────────
    builder.Services.AddSecurityServices(builder.Configuration, builder.Environment);

    // ── Application Layer (MediatR + pipeline behaviors) ────
    builder.Services.AddApplication();

    // Legacy ICommandHandler / IQueryHandler registrations (handlers not yet fully on IMediator in controllers).
    // BookAppointment / ConfirmAppointment / GetAppointment are resolved via MediatR only.
    builder.Services.AddScoped<ICommandHandler<CancelAppointmentCommand, Result>, CancelAppointmentHandler>();
    builder.Services.AddScoped<ICommandHandler<CompleteAppointmentCommand, Result>, CompleteAppointmentHandler>();
    builder.Services.AddScoped<ICommandHandler<MarkNoShowAppointmentCommand, Result>, MarkNoShowAppointmentHandler>();
    builder.Services.AddScoped<ICommandHandler<CreatePatientCommand, Result<int>>, CreatePatientHandler>();
    builder.Services.AddScoped<ICommandHandler<CreateDoctorCommand, Result<int>>, CreateDoctorHandler>();
    builder.Services.AddScoped<ICommandHandler<DeactivateDoctorCommand, Result>, DeactivateDoctorHandler>();
    builder.Services.AddScoped<ICommandHandler<ProcessPaymentCommand, Result<int>>, ProcessPaymentHandler>();
    builder.Services.AddScoped<ICommandHandler<RefundPaymentCommand, Result>, RefundPaymentHandler>();
    builder.Services.AddScoped<ICommandHandler<ForgotPasswordCommand, Result>, ForgotPasswordHandler>();
    builder.Services.AddScoped<ICommandHandler<ResetPasswordCommand, Result>, ResetPasswordHandler>();
    builder.Services.AddScoped<ICommandHandler<AnonymizePatientCommand, Result>, AnonymizePatientHandler>();
    builder.Services.AddScoped<IQueryHandler<GetRevenueReportQuery, Result<RevenueReportDto>>, GetRevenueReportHandler>();
    builder.Services.AddScoped<IQueryHandler<GetNoShowRateQuery, Result<NoShowRateDto>>, GetNoShowRateHandler>();
    builder.Services.AddScoped<IQueryHandler<GetAppointmentVolumeQuery, Result<AppointmentVolumeDto>>, GetAppointmentVolumeHandler>();

    builder.Services.AddScoped<IPaymentReconciliationService, PaymentReconciliationService>();

    // ── Adapters ────────────────────────────────────────────
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    builder.Services.AddAdaptersWithEFCorePersistence(connectionString, builder.Configuration);

    // ── Observability ───────────────────────────────────────
    builder.Services.AddObservability(builder.Configuration);

    // ── Background Services ─────────────────────────────────
    builder.Services.Configure<ReminderSettings>(
        builder.Configuration.GetSection("ReminderSettings"));
    builder.Services.AddHostedService<AppointmentReminderBackgroundService>();

    var outboxSettings = new OutboxSettings
    {
        UseOutboxForDomainEvents = builder.Configuration.GetValue<bool>("UseOutboxForDomainEvents"),
        RelayIntervalSeconds = builder.Configuration.GetValue<int>("Outbox:RelayIntervalSeconds", 10),
        MaxRetryAttempts = builder.Configuration.GetValue<int>("Outbox:MaxRetryAttempts", 5),
        BatchSize = builder.Configuration.GetValue<int>("Outbox:BatchSize", 50),
        BaseRetryDelaySeconds = builder.Configuration.GetValue<int>("Outbox:BaseRetryDelaySeconds", 5),
        MaxRetryDelaySeconds = builder.Configuration.GetValue<int>("Outbox:MaxRetryDelaySeconds", 300),
        ProcessingLeaseSeconds = builder.Configuration.GetValue<int>("Outbox:ProcessingLeaseSeconds", 120),
        CircuitBreakerFailureThreshold = builder.Configuration.GetValue<int>("Outbox:CircuitBreakerFailureThreshold", 5),
        CircuitBreakerBreakSeconds = builder.Configuration.GetValue<int>("Outbox:CircuitBreakerBreakSeconds", 60),
    };
    builder.Services.AddSingleton(outboxSettings);
    builder.Services.AddSingleton<Healthcare.Adapters.Events.OutboxMetrics>();
    builder.Services.AddHostedService<OutboxRelayService>();

    // Database migrations + optional secure admin bootstrap + gated demo seed.
    // Always registered so Production still applies migrations without enabling demo data.
    builder.Services.AddOptions<SeedingOptions>()
        .Bind(builder.Configuration.GetSection(SeedingOptions.SectionName))
        .PostConfigure(opts =>
        {
            // Back-compat: legacy top-level SeedDemoData still maps into Seeding:SeedDemoData
            if (builder.Configuration.GetSection("SeedDemoData").Exists())
                opts.SeedDemoData = builder.Configuration.GetValue<bool>("SeedDemoData");
        });

    builder.Services.AddHostedService<DatabaseSeeder>();
    Log.Information(
        "DatabaseSeeder registered (migrations always; demo/admin gated by Seeding options).");

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        var proxies = builder.Configuration.GetSection("TrustedProxies").Get<string[]>();
        var networks = builder.Configuration.GetSection("TrustedNetworks").Get<string[]>();
        if ((proxies is null || proxies.Length == 0) &&
            (networks is null || networks.Length == 0))
        {
            Log.Warning(
                "No trusted proxies or networks configured. " +
                "Rate limiting and audit IPs will use the direct RemoteIpAddress, which collapses to " +
                "one global bucket when the server sits behind a reverse proxy. " +
                "Set TrustedProxies or TrustedNetworks in production configuration.");
        }
    }

    // ── Middleware Pipeline ──────────────────────────────────
    // Order matters: forwarded headers BEFORE rate limiting / auth so client IP is correct.
    app.UseForwardedHeaders();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Healthcare API v1");
            options.RoutePrefix = string.Empty;
        });
    }
    else
    {
        app.UseHsts();
        app.MapGet("/", () => Results.Ok("Healthy"));
    }

    app.UseHttpsRedirection();

    var supportedCultures = new[] { "en", "sq" };
    var localizationOptions = new RequestLocalizationOptions()
        .SetDefaultCulture("en")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
    app.UseRequestLocalization(localizationOptions);

    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseCors("ConfiguredOrigins");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    // ── Health Check Endpoints ──────────────────────────────
    // Public liveness: status only (no detailed dependency dump).
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => true,
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    status = report.Status.ToString()
                }));
        }
    });

    // Detailed diagnostics: Admin only (avoids leaking check data to anonymous clients).
    app.MapHealthChecks("/health/details", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration.TotalMilliseconds,
                    data = e.Value.Data
                }),
                totalDuration = report.TotalDuration.TotalMilliseconds
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await context.Response.WriteAsync(result);
        }
    }).RequireAuthorization(policy => policy.RequireRole(AppRoles.Admin));

    Log.Information("Healthcare API started successfully (Environment: {Env})", app.Environment.EnvironmentName);
    if (app.Environment.IsDevelopment())
        Log.Information("Swagger UI available at: https://localhost:7039");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
