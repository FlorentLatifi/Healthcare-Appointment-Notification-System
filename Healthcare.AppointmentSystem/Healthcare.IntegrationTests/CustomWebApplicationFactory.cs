using Healthcare.Application.Ports.Authentication;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Application.Ports.Authentication;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

    public CustomWebApplicationFactory()
    {
        // Minimal hosting reads Jwt before ConfigureAppConfiguration callbacks run;
        // env vars must be set before Program.Main builds the host.
        SetEnv("Jwt__Secret", "TestSuperSecretKeyThatIsAtLeast32CharactersLong!");
        SetEnv("Jwt__Issuer", "HealthcareAPI");
        SetEnv("Jwt__Audience", "HealthcareClients");
        SetEnv("Jwt__ExpirationInMinutes", "60");
        SetEnv("Jwt__RefreshTokenExpirationInDays", "7");
        SetEnv("Stripe__SecretKey", "sk_test_placeholder");
        SetEnv("Stripe__PublishableKey", "pk_test_placeholder");
        SetEnv("RateLimiting__GlobalPermitLimit", "10000");
        SetEnv("RateLimiting__AuthPermitLimit", "10000");
        SetEnv("RateLimiting__WindowMinutes", "1");
        SetEnv("Authentication__BreachedPasswordCheckEnabled", "false");
    }

    private static void SetEnv(string key, string value)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            Environment.SetEnvironmentVariable(key, value);
    }

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
                ["Authentication:BreachedPasswordCheckEnabled"] = "false",
            });
        });

        builder.ConfigureTestServices(services =>
        {
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

        // Force container connection strings so ambient process env (e.g. from unit-test hosts)
        // cannot point EF at a non-existent local SQL Server.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            _sqlContainer.GetConnectionString());
        Environment.SetEnvironmentVariable(
            "Redis__ConnectionString",
            _redisContainer.GetConnectionString());

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HealthcareDbContext>();
        await context.Database.MigrateAsync();

        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        if (!await context.Users.AnyAsync(u => u.Username == "testadmin"))
        {
            var email = Email.Create("testadmin@test.com");
            var passwordHash = passwordHasher.HashPassword("SecurePass123!");
            var admin = User.Create("testadmin", email, passwordHash, UserRole.Admin);
            context.Users.Add(admin);
            await context.SaveChangesAsync();
        }
    }

    public new async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }
}
