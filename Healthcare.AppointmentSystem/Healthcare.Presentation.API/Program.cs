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
using Healthcare.Application.Commands.UpdateDoctor;
using Healthcare.Application.Commands.UpdatePatient;
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

SerilogConfiguration.ConfigureBootstrapLogger();

try
{
    Log.Information("Starting Healthcare API...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) =>
        configuration.ConfigureSerilog(context.Configuration, context.HostingEnvironment));

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
    builder.Services.AddScoped<ICommandHandler<UpdatePatientCommand, Result>, UpdatePatientHandler>();
    builder.Services.AddScoped<ICommandHandler<CreateDoctorCommand, Result<int>>, CreateDoctorHandler>();
    builder.Services.AddScoped<ICommandHandler<UpdateDoctorCommand, Result>, UpdateDoctorHandler>();
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

    // ── Observability (OpenTelemetry traces/metrics + Serilog logs) ──
    builder.Services.AddObservability(builder.Configuration, builder.Environment);

    // ── Background Services (resilient workers) ─────────────
    builder.Services.AddSingleton<Healthcare.Adapters.Background.IBackgroundWorkerAlert,
        Healthcare.Adapters.Background.LoggingBackgroundWorkerAlert>();
    builder.Services.AddSingleton<Healthcare.Adapters.Background.OutboxRelayHealthState>();
    builder.Services.AddSingleton<Healthcare.Adapters.Background.AppointmentReminderHealthState>();

    builder.Services.Configure<ReminderSettings>(
        builder.Configuration.GetSection(ReminderSettings.SectionName));
    builder.Services.AddSingleton<ReminderMetrics>();
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
        BatchPollyRetryAttempts = builder.Configuration.GetValue<int>("Outbox:BatchPollyRetryAttempts", 2),
        BatchPollyRetryBaseDelaySeconds = builder.Configuration.GetValue<int>("Outbox:BatchPollyRetryBaseDelaySeconds", 2),
        ShutdownTimeoutSeconds = builder.Configuration.GetValue<int>("Outbox:ShutdownTimeoutSeconds", 30),
        UnhealthyIfNoSuccessMinutes = builder.Configuration.GetValue<int>("Outbox:UnhealthyIfNoSuccessMinutes", 15),
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

    // Rate limiting / audit IPs + Stripe webhook signing: fail closed in Production;
    // non-Production logs a warning only (same convention as Seeding demo-data gates).
    if (!ProductionStartupGuards.EnsureTrustedProxyConfigOrThrow(app.Environment, builder.Configuration)
        && !app.Environment.IsDevelopment())
    {
        Log.Warning(
            "No trusted proxies or networks configured. " +
            "Rate limiting and audit IPs will use the direct RemoteIpAddress, which collapses to " +
            "one global bucket when the server sits behind a reverse proxy. " +
            "Set TrustedProxies or TrustedNetworks in production configuration.");
    }

    if (!ProductionStartupGuards.EnsureStripeWebhookSecretOrThrow(app.Environment, builder.Configuration)
        && !app.Environment.IsDevelopment())
    {
        Log.Warning(
            "Stripe:WebhookSecret is not configured. " +
            "Webhook signature verification cannot fail closed until a signing secret is set. " +
            "Production requires Stripe:WebhookSecret (env Stripe__WebhookSecret).");
    }

    // ── Middleware Pipeline ──────────────────────────────────
    // Order: forwarded headers → security headers (early OnStarting) → exception → correlation → …
    // Rate limiting stays after CORS/auth path; SecurityHeaders does not alter request flow.
    app.UseForwardedHeaders();

    // Register security headers as early as practical so all responses (success, 4xx/5xx, health)
    // get HSTS/CSP/XFO/etc. via Response.OnStarting.
    app.UseMiddleware<SecurityHeadersMiddleware>();

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("CorrelationId",
                httpContext.Items.TryGetValue(Healthcare.Application.Observability.CorrelationContext.HttpContextItemKey, out var cid)
                    ? cid
                    : null);
            diagnosticContext.Set("UserId", httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
            diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString());
        };
        options.GetLevel = (ctx, elapsed, ex) =>
            ex is not null || ctx.Response.StatusCode >= 500
                ? Serilog.Events.LogEventLevel.Error
                : ctx.Response.StatusCode >= 400
                    ? Serilog.Events.LogEventLevel.Warning
                    : Serilog.Events.LogEventLevel.Information;
    });

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
        // Built-in HSTS (max-age 365d, includeSubDomains, preload) — configured in SecurityServicesConfiguration.
        app.UseHsts();
        app.MapGet("/", () => Results.Ok(new { status = "Healthy" }));
    }

    app.UseHttpsRedirection();

    var supportedCultures = new[] { "en", "sq" };
    var localizationOptions = new RequestLocalizationOptions()
        .SetDefaultCulture("en")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
    app.UseRequestLocalization(localizationOptions);

    app.UseCors("ConfiguredOrigins");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    // ── Health: /health/live, /health/ready, /health, /health/details ──
    app.MapHealthcareHealthEndpoints();

    Log.Information(
        "Healthcare API started successfully Environment={Env} Service={Service}",
        app.Environment.EnvironmentName,
        ObservabilityConfiguration.ServiceName);
    if (app.Environment.IsDevelopment())
        Log.Information("Swagger UI available at: https://localhost:7039");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    // Re-throw so Production fail-fast config errors (JWT, CORS, trusted proxies, etc.)
    // surface a non-zero exit code and are visible to WebApplicationFactory / orchestrators.
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
