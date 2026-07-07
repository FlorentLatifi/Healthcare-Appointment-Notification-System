using Healthcare.Adapters;
using Healthcare.Adapters.Events;
using Healthcare.Adapters.Factories;
using Healthcare.Adapters.Services;
using Healthcare.Application.Commands.BookAppointment;
using Healthcare.Application.Commands.CancelAppointment;
using Healthcare.Application.Commands.CompleteAppointment;
using Healthcare.Application.Commands.ConfirmAppointment;
using Healthcare.Application.Commands.CreateDoctor;
using Healthcare.Application.Commands.CreatePatient;
using Healthcare.Application.Commands.DeactivateDoctor;
using Healthcare.Application.Commands.MarkNoShowAppointment;
using Healthcare.Application.Commands.ProcessPayment;
using Healthcare.Application.Commands.RefundPayment;
using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Facades;
using Healthcare.Application.Ports.Factories;
using Healthcare.Application.Ports.Payments;
using Healthcare.Application.Queries.Analytics;
using Healthcare.Application.Queries.GetAppointment;
using Healthcare.Application.Queries.GetAppointmentsByPatient;
using Healthcare.Application.Services;
using Healthcare.Adapters.Persistence.EntityFramework;
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

    // ── API Infrastructure ──────────────────────────────────
    builder.Services.AddApiInfrastructure();

    // ── Security ────────────────────────────────────────────
    builder.Services.AddSecurityServices(builder.Configuration, builder.Environment);

    // ── Application Layer ───────────────────────────────────
    builder.Services.AddScoped<ICommandHandler<BookAppointmentCommand, Result<int>>, BookAppointmentHandler>();
    builder.Services.AddScoped<ICommandHandler<ConfirmAppointmentCommand, Result>, ConfirmAppointmentHandler>();
    builder.Services.AddScoped<ICommandHandler<CancelAppointmentCommand, Result>, CancelAppointmentHandler>();
    builder.Services.AddScoped<ICommandHandler<CompleteAppointmentCommand, Result>, CompleteAppointmentHandler>();
    builder.Services.AddScoped<ICommandHandler<MarkNoShowAppointmentCommand, Result>, MarkNoShowAppointmentHandler>();
    builder.Services.AddScoped<ICommandHandler<CreatePatientCommand, Result<int>>, CreatePatientHandler>();
    builder.Services.AddScoped<ICommandHandler<CreateDoctorCommand, Result<int>>, CreateDoctorHandler>();
    builder.Services.AddScoped<ICommandHandler<DeactivateDoctorCommand, Result>, DeactivateDoctorHandler>();
    builder.Services.AddScoped<ICommandHandler<ProcessPaymentCommand, Result<int>>, ProcessPaymentHandler>();
    builder.Services.AddScoped<ICommandHandler<RefundPaymentCommand, Result>, RefundPaymentHandler>();
    builder.Services.AddScoped<IQueryHandler<GetRevenueReportQuery, Result<RevenueReportDto>>, GetRevenueReportHandler>();
    builder.Services.AddScoped<IQueryHandler<GetNoShowRateQuery, Result<NoShowRateDto>>, GetNoShowRateHandler>();
    builder.Services.AddScoped<IQueryHandler<GetAppointmentVolumeQuery, Result<AppointmentVolumeDto>>, GetAppointmentVolumeHandler>();
    builder.Services.AddScoped<IQueryHandler<GetAppointmentQuery, Result<AppointmentDto>>, GetAppointmentHandler>();
    builder.Services.AddScoped<IQueryHandler<GetAppointmentsByPatientQuery, Result<IEnumerable<AppointmentDto>>>, GetAppointmentsByPatientHandler>();
    builder.Services.AddScoped<IAppointmentFacade, AppointmentFacade>();
    builder.Services.AddScoped<IPaymentReconciliationService, PaymentReconciliationService>();

    // ── Adapters ────────────────────────────────────────────
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    builder.Services.AddAdaptersWithEFCorePersistence(connectionString, builder.Configuration);
    builder.Services.AddSingleton<IHealthcareRepositoryFactory, InMemoryRepositoryFactory>();

    // ── Observability ───────────────────────────────────────
    builder.Services.AddObservability();

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
    };
    builder.Services.AddSingleton(outboxSettings);
    builder.Services.AddHostedService<OutboxRelayService>();

    var seedDemoData = builder.Configuration.GetValue<bool>("SeedDemoData");
    if (seedDemoData)
    {
        Log.Information("SeedDemoData is enabled — registering DatabaseSeeder.");
        builder.Services.AddHostedService<DatabaseSeeder>();
    }

    var app = builder.Build();

    // ── Middleware Pipeline ──────────────────────────────────
    app.UseForwardedHeaders();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Healthcare API v1");
        options.RoutePrefix = string.Empty;
    });

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
    app.MapHealthChecks("/health");
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
    });

    Log.Information("Healthcare API started successfully");
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
