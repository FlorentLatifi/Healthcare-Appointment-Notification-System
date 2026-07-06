using Asp.Versioning;
using FluentValidation;
using FluentValidation.AspNetCore;
using Healthcare.Adapters;
using Healthcare.Presentation.API.HealthChecks;
using Healthcare.Presentation.API.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Healthcare.Application.Commands.BookAppointment;
using Healthcare.Application.Commands.CancelAppointment;
using Healthcare.Application.Commands.CompleteAppointment;
using Healthcare.Application.Commands.ConfirmAppointment;
using Healthcare.Application.Commands.MarkNoShowAppointment;
using Healthcare.Application.Commands.CreatePatient;
using Healthcare.Application.Commands.CreateDoctor;
using Healthcare.Application.Commands.DeactivateDoctor;
using Healthcare.Application.Common;
using Healthcare.Presentation.API.Filters;
using Healthcare.Presentation.API.Middleware;
using Serilog;
using Healthcare.Adapters.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Healthcare.Application.Commands.ProcessPayment;
using Healthcare.Application.Commands.RefundPayment;
using Healthcare.Application.Queries.Analytics;
using Healthcare.Application.Queries.GetAppointment;
using Healthcare.Application.Queries.GetAppointmentsByPatient;
using Healthcare.Application.DTOs;
using Healthcare.Adapters.Factories;
using Healthcare.Application.Ports.Factories;
using Healthcare.Application.Ports.Facades;
using Healthcare.Application.Ports.Payments;
using Healthcare.Application.Services;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Healthcare.Presentation.API.Resources;
using Healthcare.Presentation.API.Responses;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// ============================================
// SERILOG CONFIGURATION
// ============================================
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {CorrelationId}{NewLine}{Exception}")
    .WriteTo.File("logs/healthcare-api-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting Healthcare API...");

    var builder = WebApplication.CreateBuilder(args);

    // ============================================
    // LOGGING
    // ============================================
    builder.Host.UseSerilog();

    // ============================================
    // CONTROLLERS & VALIDATION
    // ============================================
    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ValidationFilter>();
    });

    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    // ============================================
    // API VERSIONING
    // ============================================
    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    // ============================================
    // SWAGGER/OPENAPI
    // ============================================
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new()
        {
            Title = "Healthcare Appointment API",
            Version = "v1.0",
            Description = "RESTful API for managing healthcare appointments",
            Contact = new()
            {
                Name = "Healthcare Team",
                Email = "support@healthcareclinic.com"
            }
        });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter a valid JWT Bearer token."
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });

        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    });

    // ============================================
    // APPLICATION LAYER (COMMAND HANDLERS)
    // ============================================
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

    // ============================================
    // APPLICATION LAYER (QUERY HANDLERS)
    // ============================================
    builder.Services.AddScoped<IQueryHandler<GetRevenueReportQuery, Result<RevenueReportDto>>, GetRevenueReportHandler>();
    builder.Services.AddScoped<IQueryHandler<GetNoShowRateQuery, Result<NoShowRateDto>>, GetNoShowRateHandler>();
    builder.Services.AddScoped<IQueryHandler<GetAppointmentVolumeQuery, Result<AppointmentVolumeDto>>, GetAppointmentVolumeHandler>();
    builder.Services.AddScoped<IQueryHandler<GetAppointmentQuery, Result<AppointmentDto>>, GetAppointmentHandler>();
    builder.Services.AddScoped<IQueryHandler<GetAppointmentsByPatientQuery, Result<IEnumerable<AppointmentDto>>>, GetAppointmentsByPatientHandler>();
    // ── FACADE PATTERN (Structural) ──────────────────────────
    builder.Services.AddScoped<IAppointmentFacade, AppointmentFacade>();

    // ── PAYMENT RECONCILIATION SERVICE ──────────────────────
    builder.Services.AddScoped<IPaymentReconciliationService, PaymentReconciliationService>();
    // ─────────────────────────────────────────────────────────
    // ============================================
    // HEALTH CHECKS
    // ============================================
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("database", failureStatus: HealthStatus.Unhealthy, tags: new[] { "db", "sql", "critical" })
        .AddCheck<RedisHealthCheck>("redis", failureStatus: HealthStatus.Degraded, tags: new[] { "cache", "redis", "locking" })
        .AddCheck<MemoryHealthCheck>("memory", failureStatus: HealthStatus.Degraded, tags: new[] { "memory", "performance" });

    // ============================================
    // LOCALIZATION
    // ============================================
    builder.Services.AddLocalization();

    // ============================================
    // JWT AUTHENTICATION
    // ============================================
    var jwtSettings = JwtSettings.FromConfiguration(builder.Configuration);
    builder.Services.AddSingleton(jwtSettings);

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret))
        };
    });

    builder.Services.AddAuthorization();

    // ============================================
    // FORWARDED HEADERS (trusted proxy support)
    // ============================================
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        var trustedProxies = builder.Configuration.GetSection("TrustedProxies").Get<string[]>();
        if (trustedProxies is not null)
        {
            foreach (var proxy in trustedProxies)
            {
                if (System.Net.IPAddress.TryParse(proxy, out var ip))
                {
                    options.KnownProxies.Add(ip);
                }
            }
        }

        var trustedNetworks = builder.Configuration.GetSection("TrustedNetworks").Get<string[]>();
        if (trustedNetworks is not null)
        {
            foreach (var network in trustedNetworks)
            {
                // Parse CIDR "10.0.0.0/8" — ASP.NET supports it natively
                var parts = network.Split('/');
                if (parts.Length == 2
                    && System.Net.IPAddress.TryParse(parts[0], out var networkIp)
                    && int.TryParse(parts[1], out var prefixLength))
                {
                    options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(networkIp, prefixLength));
                }
            }
        }
    });

    // ============================================
    // RATE LIMITING
    // ============================================
    var globalRateLimit = builder.Configuration.GetValue<int>("RateLimiting:GlobalPermitLimit", 100);
    var authRateLimit = builder.Configuration.GetValue<int>("RateLimiting:AuthPermitLimit", 5);
    var rateLimitWindowMinutes = builder.Configuration.GetValue<int>("RateLimiting:WindowMinutes", 1);

    builder.Services.AddRateLimiter(options =>
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ipAddress,
                factory: partition => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = globalRateLimit,
                    QueueLimit = 0,
                    Window = TimeSpan.FromMinutes(rateLimitWindowMinutes)
                });
        });

        options.AddPolicy("AuthPolicy", httpContext =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ipAddress,
                factory: partition => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = authRateLimit,
                    QueueLimit = 0,
                    Window = TimeSpan.FromMinutes(rateLimitWindowMinutes)
                });
        });

        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.ContentType = "application/json";

            var apiResponse = ApiResponse.ErrorResponse(
                "Too many requests. Please try again later.",
                "Rate limit exceeded"
            );

            var json = System.Text.Json.JsonSerializer.Serialize(apiResponse, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

            await context.HttpContext.Response.WriteAsync(json, cancellationToken);
        };
    });

    // ============================================
    // ADAPTERS LAYER
    // ============================================
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    builder.Services.AddAdaptersWithEFCorePersistence(connectionString, builder.Configuration);
    // ── ABSTRACT FACTORY (Creational Pattern) ───────────────
    // Registers which factory family to use.
    // Switch to EFCoreRepositoryFactory for production.
    builder.Services.AddSingleton<IHealthcareRepositoryFactory,
        InMemoryRepositoryFactory>();
    // ============================================
    // OPEN TELEMETRY (Distributed Tracing)
    // ============================================
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("HealthcareAPI"))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddConsoleExporter());
    // ── PRODUCTION ───────────────────────────────────
    // Replace .AddConsoleExporter() with:
    // .AddOtlpExporter(options => options.Endpoint = new Uri("http://jaeger:4317"))
    // Requires OpenTelemetry.Exporter.OpenTelemetryProtocol package.

    // ============================================
    // CORS
    // ============================================
    var allowedOrigins = builder.Configuration
        .GetSection("AllowedOrigins")
        .Get<string[]>() ?? Array.Empty<string>();

    if (builder.Environment.IsDevelopment())
    {
        var devOrigins = new List<string>(allowedOrigins);
        foreach (var origin in new[] { "http://localhost:5173", "https://localhost:5173" })
        {
            if (!devOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
            {
                devOrigins.Add(origin);
            }
        }

        allowedOrigins = devOrigins.ToArray();
    }
    else if (allowedOrigins.Length == 0)
    {
        throw new InvalidOperationException(
            "No CORS origins configured. Set 'AllowedOrigins' in appsettings.json or environment variables for Production.");
    }

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ConfiguredOrigins", policy =>
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });

    // ============================================
    // BACKGROUND SERVICES
    // ============================================
    builder.Services.Configure<ReminderSettings>(
        builder.Configuration.GetSection("ReminderSettings"));
    builder.Services.AddHostedService<AppointmentReminderBackgroundService>();

    // ============================================
    // DATABASE SEEDING (dev/demo only)
    // ============================================
    var seedDemoData = builder.Configuration.GetValue<bool>("SeedDemoData");
    if (seedDemoData)
    {
        Log.Information("SeedDemoData is enabled — registering DatabaseSeeder.");
        builder.Services.AddHostedService<DatabaseSeeder>();
    }

    // ============================================
    // BUILD APP
    // ============================================
    var app = builder.Build();

    // ============================================
    // MIDDLEWARE PIPELINE
    // ============================================
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

    // ============================================
    // HEALTH CHECK ENDPOINTS
    // ============================================
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
