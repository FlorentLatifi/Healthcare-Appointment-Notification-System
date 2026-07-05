using Asp.Versioning;
using FluentValidation;
using FluentValidation.AspNetCore;
using Healthcare.Adapters;
using Healthcare.Presentation.API.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Healthcare.Application.Commands.BookAppointment;
using Healthcare.Application.Commands.CancelAppointment;
using Healthcare.Application.Commands.ConfirmAppointment;
using Healthcare.Application.Commands.CreatePatient;
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
using Healthcare.Adapters.Factories;
using Healthcare.Application.Ports.Factories;
using Healthcare.Application.Ports.Facades;
using Healthcare.Application.Services;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Healthcare.Presentation.API.Responses;

// ============================================
// SERILOG CONFIGURATION
// ============================================
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
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
    builder.Services.AddScoped<ICommandHandler<CreatePatientCommand, Result<int>>, CreatePatientHandler>();
    builder.Services.AddScoped<ICommandHandler<ProcessPaymentCommand, Result<int>>, ProcessPaymentHandler>();
    builder.Services.AddScoped<ICommandHandler<RefundPaymentCommand, Result>, RefundPaymentHandler>();
    // ── FACADE PATTERN (Structural) ──────────────────────────
    builder.Services.AddScoped<IAppointmentFacade, AppointmentFacade>();
    // ─────────────────────────────────────────────────────────
    // ============================================
    // HEALTH CHECKS
    // ============================================
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("database", failureStatus: HealthStatus.Unhealthy, tags: new[] { "db", "sql", "critical" })
        .AddCheck<RedisHealthCheck>("redis", failureStatus: HealthStatus.Degraded, tags: new[] { "cache", "redis", "locking" })
        .AddCheck<MemoryHealthCheck>("memory", failureStatus: HealthStatus.Degraded, tags: new[] { "memory", "performance" });

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
    // RATE LIMITING
    // ============================================
    builder.Services.AddRateLimiter(options =>
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString()
                ?? httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ipAddress,
                factory: partition => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 100,
                    QueueLimit = 0,
                    Window = TimeSpan.FromMinutes(1)
                });
        });

        options.AddPolicy("AuthPolicy", httpContext =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString()
                ?? httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ipAddress,
                factory: partition => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 5,
                    QueueLimit = 0,
                    Window = TimeSpan.FromMinutes(1)
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
    // BUILD APP
    // ============================================
    var app = builder.Build();

    // ============================================
    // MIDDLEWARE PIPELINE
    // ============================================
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Healthcare API v1");
        options.RoutePrefix = string.Empty;
    });

    app.UseHttpsRedirection();
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
