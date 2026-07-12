using FluentAssertions;
using Healthcare.Adapters.Caching;
using Healthcare.Adapters.Locking;
using Healthcare.Adapters.Persistence.InMemory;
using Healthcare.Adapters.Services;
using Healthcare.Application.Ports.Authentication;
using Healthcare.Application.Ports.Caching;
using Healthcare.Application.Ports.Locking;
using Healthcare.Application.Ports.Payments;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using StackExchange.Redis;

namespace Healthcare.UnitTests.Presentation;

/// <summary>
/// Production must fail fast without TrustedProxies / TrustedNetworks so rate-limit
/// and audit IP partitioning cannot silently collapse behind a reverse proxy.
/// </summary>
public sealed class TrustedProxyStartupTests
{
    [Fact]
    public void Production_WithoutTrustedProxyConfig_FailsToStart()
    {
        using var factory = new ProductionHostFactory(configureTrustedProxy: false);

        var act = () => factory.CreateClient();

        // WebApplicationFactory wraps the host-build failure; message is on an inner exception.
        var ex = act.Should().Throw<Exception>().Which;
        FlattenExceptions(ex).Select(e => e.Message)
            .Should().Contain(m => m.Contains("TrustedProxies", StringComparison.Ordinal)
                                   && m.Contains("TrustedNetworks", StringComparison.Ordinal));
    }

    private static IEnumerable<Exception> FlattenExceptions(Exception ex)
    {
        yield return ex;
        if (ex is AggregateException agg)
        {
            foreach (var inner in agg.Flatten().InnerExceptions)
            foreach (var nested in FlattenExceptions(inner))
                yield return nested;
        }
        else if (ex.InnerException is not null)
        {
            foreach (var nested in FlattenExceptions(ex.InnerException))
                yield return nested;
        }
    }

    [Fact]
    public void Production_WithTrustedProxies_StartsSuccessfully()
    {
        using var factory = new ProductionHostFactory(configureTrustedProxy: true);

        using var client = factory.CreateClient();
        client.Should().NotBeNull();
    }

    [Fact]
    public void Development_WithoutTrustedProxyConfig_StartsSuccessfully()
    {
        using var factory = new DevelopmentHostFactory();

        using var client = factory.CreateClient();
        client.Should().NotBeNull();
    }

    private static void ReplaceInfrastructureWithInMemory(IServiceCollection services)
    {
        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType.Name == "DbContextOptions`1")
                services.RemoveAt(i);
        }

        services.RemoveAll<Healthcare.Adapters.Persistence.EntityFramework.HealthcareDbContext>();
        services.RemoveAll<IAppointmentRepository>();
        services.RemoveAll<IPatientRepository>();
        services.RemoveAll<IDoctorRepository>();
        services.RemoveAll<IUserRepository>();
        services.RemoveAll<IPaymentRepository>();
        services.RemoveAll<IAuditLogRepository>();
        services.RemoveAll<IUserSessionRepository>();
        services.RemoveAll<IUnitOfWork>();
        services.RemoveAll<IConnectionMultiplexer>();
        services.RemoveAll<IDistributedLockService>();
        services.RemoveAll<IDoctorCacheService>();
        services.RemoveAll<ICacheService>();
        services.RemoveAll<IAvailabilityCacheService>();
        services.RemoveAll<CacheSettings>();
        services.RemoveAll<IAppointmentCodeGenerator>();
        services.RemoveAll<IPaymentGateway>();
        services.RemoveAll<IBreachedPasswordChecker>();

        services.AddSingleton<IAppointmentRepository, InMemoryAppointmentRepository>();
        services.AddSingleton<IPatientRepository, InMemoryPatientRepository>();
        services.AddSingleton<IDoctorRepository, InMemoryDoctorRepository>();
        services.AddSingleton<IUserRepository, InMemoryUserRepository>();
        services.AddSingleton<IPaymentRepository, InMemoryPaymentRepository>();
        services.AddSingleton<IAuditLogRepository, InMemoryAuditLogRepository>();
        services.AddSingleton<IUserSessionRepository, InMemoryUserSessionRepository>();
        services.AddSingleton<IUnitOfWork, InMemoryUnitOfWork>();
        services.AddSingleton<IDistributedLockService, InMemoryLockService>();
        services.AddSingleton(new CacheSettings());
        services.AddSingleton<ICacheService, InMemoryCacheService>();
        services.AddSingleton<IDoctorCacheService, DoctorCacheService>();
        services.AddSingleton<IAvailabilityCacheService, AvailabilityCacheService>();
        services.AddSingleton<IAppointmentCodeGenerator, AppointmentCodeGenerator>();
        services.AddScoped<IPaymentGateway>(_ => Mock.Of<IPaymentGateway>());
        services.AddScoped<IBreachedPasswordChecker>(_ =>
        {
            var m = new Mock<IBreachedPasswordChecker>();
            m.Setup(x => x.IsPasswordBreachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            return m.Object;
        });
        services.AddSingleton<IConnectionMultiplexer>(_ => Mock.Of<IConnectionMultiplexer>());
    }

    private sealed class ProductionHostFactory : WebApplicationFactory<Program>
    {
        private readonly bool _configureTrustedProxy;

        public ProductionHostFactory(bool configureTrustedProxy)
        {
            _configureTrustedProxy = configureTrustedProxy;

            // Read before Program.Main / AddSecurityServices (minimal hosting).
            Environment.SetEnvironmentVariable("Jwt__Secret", "TestSuperSecretKeyForTrustedProxyStartup32!");
            Environment.SetEnvironmentVariable("Jwt__Issuer", "HealthcareAPI");
            Environment.SetEnvironmentVariable("Jwt__Audience", "HealthcareClients");
            Environment.SetEnvironmentVariable("Jwt__ExpirationInMinutes", "60");
            Environment.SetEnvironmentVariable("Stripe__SecretKey", "sk_test_proxy_startup");
            Environment.SetEnvironmentVariable("Stripe__PublishableKey", "pk_test_proxy_startup");
            // Production also requires webhook secret (independent fail-fast check).
            Environment.SetEnvironmentVariable("Stripe__WebhookSecret", "whsec_test_proxy_startup");
            Environment.SetEnvironmentVariable("RateLimiting__GlobalPermitLimit", "10000");
            Environment.SetEnvironmentVariable("RateLimiting__AuthPermitLimit", "10000");
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__DefaultConnection",
                "Server=.;Database=ProxyStartup_Unused;Trusted_Connection=true;");
            Environment.SetEnvironmentVariable("Redis__ConnectionString", "localhost:6379");
            Environment.SetEnvironmentVariable("AllowedOrigins__0", "https://app.example.com");
            Environment.SetEnvironmentVariable("Authentication__BreachedPasswordCheckEnabled", "false");
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");

            if (configureTrustedProxy)
            {
                Environment.SetEnvironmentVariable("TrustedProxies__0", "10.0.0.1");
                Environment.SetEnvironmentVariable("TrustedNetworks__0", "10.0.0.0/8");
            }
            else
            {
                // Clear ambient proxy config so Production fails closed.
                Environment.SetEnvironmentVariable("TrustedProxies__0", null);
                Environment.SetEnvironmentVariable("TrustedNetworks__0", null);
                Environment.SetEnvironmentVariable("TrustedProxies", null);
                Environment.SetEnvironmentVariable("TrustedNetworks", null);
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["Jwt:Secret"] = "TestSuperSecretKeyForTrustedProxyStartup32!",
                    ["Jwt:Issuer"] = "HealthcareAPI",
                    ["Jwt:Audience"] = "HealthcareClients",
                    ["AllowedOrigins:0"] = "https://app.example.com",
                    ["Stripe:SecretKey"] = "sk_test_proxy_startup",
                    ["Stripe:PublishableKey"] = "pk_test_proxy_startup",
                    ["Stripe:WebhookSecret"] = "whsec_test_proxy_startup",
                    ["Authentication:BreachedPasswordCheckEnabled"] = "false",
                    ["Seeding:SeedDemoData"] = "false",
                    ["Seeding:BootstrapAdmin:Enabled"] = "false",
                };

                if (_configureTrustedProxy)
                {
                    values["TrustedProxies:0"] = "10.0.0.1";
                    values["TrustedNetworks:0"] = "10.0.0.0/8";
                }

                config.AddInMemoryCollection(values);
            });

            builder.ConfigureTestServices(ReplaceInfrastructureWithInMemory);
        }
    }

    private sealed class DevelopmentHostFactory : WebApplicationFactory<Program>
    {
        public DevelopmentHostFactory()
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            Environment.SetEnvironmentVariable("Jwt__Secret", "TestSuperSecretKeyForTrustedProxyStartup32!");
            Environment.SetEnvironmentVariable("Jwt__Issuer", "HealthcareAPI");
            Environment.SetEnvironmentVariable("Jwt__Audience", "HealthcareClients");
            Environment.SetEnvironmentVariable("Stripe__SecretKey", "sk_test_proxy_startup");
            Environment.SetEnvironmentVariable("Stripe__PublishableKey", "pk_test_proxy_startup");
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__DefaultConnection",
                "Server=.;Database=ProxyDev_Unused;Trusted_Connection=true;");
            Environment.SetEnvironmentVariable("Redis__ConnectionString", "localhost:6379");
            Environment.SetEnvironmentVariable("Authentication__BreachedPasswordCheckEnabled", "false");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Secret"] = "TestSuperSecretKeyForTrustedProxyStartup32!",
                    ["Stripe:SecretKey"] = "sk_test_proxy_startup",
                    ["Stripe:PublishableKey"] = "pk_test_proxy_startup",
                    ["Authentication:BreachedPasswordCheckEnabled"] = "false",
                    ["Seeding:SeedDemoData"] = "false",
                    ["Seeding:BootstrapAdmin:Enabled"] = "false",
                });
            });
            builder.ConfigureTestServices(ReplaceInfrastructureWithInMemory);
        }
    }
}
