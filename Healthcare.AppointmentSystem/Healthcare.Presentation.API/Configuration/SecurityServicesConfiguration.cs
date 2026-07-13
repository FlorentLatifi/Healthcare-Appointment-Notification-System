using Healthcare.Adapters.Authentication;
using Healthcare.Application.Ports.Authentication;
using Healthcare.Presentation.API.Responses;
using Healthcare.Presentation.API.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace Healthcare.Presentation.API.Configuration;

public static class SecurityServicesConfiguration
{
    public static IServiceCollection AddSecurityServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // Required so only configured proxies can influence RemoteIpAddress (rate-limit / audit IP).
            options.ForwardLimit = 1;

            var trustedProxies = configuration.GetSection("TrustedProxies").Get<string[]>();
            if (trustedProxies is not null)
            {
                foreach (var proxy in trustedProxies)
                {
                    if (System.Net.IPAddress.TryParse(proxy, out var ip))
                        options.KnownProxies.Add(ip);
                }
            }

            var trustedNetworks = configuration.GetSection("TrustedNetworks").Get<string[]>();
            if (trustedNetworks is not null)
            {
                foreach (var network in trustedNetworks)
                {
                    var parts = network.Split('/');
                    if (parts.Length == 2
                        && System.Net.IPAddress.TryParse(parts[0], out var networkIp)
                        && int.TryParse(parts[1], out var prefixLength))
                    {
                        options.KnownNetworks.Add(
                            new Microsoft.AspNetCore.HttpOverrides.IPNetwork(networkIp, prefixLength));
                    }
                }
            }
        });

        var jwtSettings = JwtSettings.FromConfiguration(configuration);
        services.AddSingleton(jwtSettings);
        services.AddHttpContextAccessor();
        // Prefer HTTP ambient context over Application's NullAuditContext singleton.
        services.AddScoped<Healthcare.Application.Ports.Audit.IAuditContext, Security.HttpAuditContext>();
        services.AddScoped<SecurityAuditWriter>();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = true;
            options.RequireHttpsMetadata = !environment.IsDevelopment();
            options.SaveToken = false;
            options.TokenValidationParameters = JwtTokenValidation.CreateParameters(jwtSettings);
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    context.NoResult();
                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization();

        if (!environment.IsDevelopment())
        {
            services.AddHsts(options =>
            {
                options.Preload = true;
                options.IncludeSubDomains = true;
                options.MaxAge = TimeSpan.FromDays(365);
            });
        }

        var globalRateLimit = configuration.GetValue<int>("RateLimiting:GlobalPermitLimit", 100);
        var authRateLimit = configuration.GetValue<int>("RateLimiting:AuthPermitLimit", 5);
        var rateLimitWindowMinutes = configuration.GetValue<int>("RateLimiting:WindowMinutes", 1);
        var window = TimeSpan.FromMinutes(Math.Max(1, rateLimitWindowMinutes));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Global: authenticated users partitioned by user id (fair behind corporate NAT);
            // anonymous by client IP (after forwarded-headers).
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ClientIpResolver.GetRateLimitPartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = globalRateLimit,
                        QueueLimit = 0,
                        Window = window
                    }));

            // Stricter bucket for login/register/password endpoints (IP-based).
            options.AddPolicy("AuthPolicy", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ClientIpResolver.GetAnonymousAuthPartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = authRateLimit,
                        QueueLimit = 0,
                        Window = window
                    }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;
                response.ContentType = "application/json";
                response.Headers.RetryAfter = Math.Max(1, (int)window.TotalSeconds).ToString();

                var apiResponse = ApiResponse.ErrorResponse(
                    "Too many requests. Please try again later.",
                    "Rate limit exceeded");

                var json = System.Text.Json.JsonSerializer.Serialize(apiResponse,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                    });

                await response.WriteAsync(json, cancellationToken);
            };
        });

        var allowedOrigins = configuration
            .GetSection("AllowedOrigins")
            .Get<string[]>() ?? Array.Empty<string>();

        if (environment.IsDevelopment())
        {
            var devOrigins = new List<string>(allowedOrigins);
            foreach (var origin in new[] { "http://localhost:5173", "https://localhost:5173" })
            {
                if (!devOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
                    devOrigins.Add(origin);
            }

            allowedOrigins = devOrigins.ToArray();
        }
        else if (allowedOrigins.Length == 0)
        {
            throw new InvalidOperationException(
                "No CORS origins configured. Set 'AllowedOrigins' in appsettings.json or environment variables for Production.");
        }

        services.AddCors(options =>
        {
            options.AddPolicy("ConfiguredOrigins", policy =>
            {
                // Explicit methods/headers — avoid AllowAny* for credentialed PHI APIs.
                policy.WithOrigins(allowedOrigins)
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                    .WithHeaders(
                        "Authorization",
                        "Content-Type",
                        "Accept",
                        "X-Correlation-Id",
                        "X-Requested-With")
                    .WithExposedHeaders("X-Correlation-Id", "Retry-After")
                    .AllowCredentials();
            });
        });

        return services;
    }
}
