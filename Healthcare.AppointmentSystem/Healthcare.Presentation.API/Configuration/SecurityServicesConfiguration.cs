using Healthcare.Application.Ports.Authentication;
using Healthcare.Presentation.API.Responses;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
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

            var trustedProxies = configuration.GetSection("TrustedProxies").Get<string[]>();
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
                        options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(networkIp, prefixLength));
                    }
                }
            }
        });

        var jwtSettings = JwtSettings.FromConfiguration(configuration);
        services.AddSingleton(jwtSettings);

        services.AddAuthentication(options =>
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

        services.AddAuthorization();

        var globalRateLimit = configuration.GetValue<int>("RateLimiting:GlobalPermitLimit", 100);
        var authRateLimit = configuration.GetValue<int>("RateLimiting:AuthPermitLimit", 5);
        var rateLimitWindowMinutes = configuration.GetValue<int>("RateLimiting:WindowMinutes", 1);

        services.AddRateLimiter(options =>
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

        var allowedOrigins = configuration
            .GetSection("AllowedOrigins")
            .Get<string[]>() ?? Array.Empty<string>();

        if (environment.IsDevelopment())
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

        services.AddCors(options =>
        {
            options.AddPolicy("ConfiguredOrigins", policy =>
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
        });

        return services;
    }
}
