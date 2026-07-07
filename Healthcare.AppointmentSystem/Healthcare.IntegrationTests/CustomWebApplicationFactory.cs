using Healthcare.Adapters.Authentication;
using Healthcare.Adapters.Factories;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Application.Ports.Factories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Testcontainers.MsSql;
using Testcontainers.Redis;

namespace Healthcare.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong!Passw0rd")
        .Build();

    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var sqlConnectionString = _sqlContainer.GetConnectionString();
        var redisConnectionString = _redisContainer.GetConnectionString();

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "TestSuperSecretKeyThatIsAtLeast32CharactersLong!",
                ["Jwt:Issuer"] = "HealthcareAPI",
                ["Jwt:Audience"] = "HealthcareClients",
                ["Jwt:ExpirationInMinutes"] = "60",
                ["Jwt:RefreshTokenExpirationInDays"] = "7",
                ["ConnectionStrings:DefaultConnection"] = sqlConnectionString,
                ["Redis:ConnectionString"] = redisConnectionString,
                ["Redis:InstanceName"] = "HealthcareApp:Test:",
                ["Redis:DefaultLockExpirationSeconds"] = "5",
                ["Stripe:SecretKey"] = "sk_test_placeholder",
                ["Stripe:PublishableKey"] = "pk_test_placeholder",
                ["RateLimiting:GlobalPermitLimit"] = "10000",
                ["RateLimiting:AuthPermitLimit"] = "10000",
                ["RateLimiting:WindowMinutes"] = "1",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHealthcareRepositoryFactory>();
            services.AddSingleton<IHealthcareRepositoryFactory>(sp =>
            {
                var context = sp.GetRequiredService<HealthcareDbContext>();
                var logger = sp.GetRequiredService<ILogger<EFCoreRepositoryFactory>>();
                return new EFCoreRepositoryFactory(context, logger);
            });

            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(new ConfigurationOptions
                {
                    EndPoints = { redisConnectionString },
                    AbortOnConnectFail = false,
                    ConnectTimeout = 5000,
                    SyncTimeout = 5000,
                }));
        });
    }

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
        await _redisContainer.StartAsync();

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HealthcareDbContext>();
        await context.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }
}
