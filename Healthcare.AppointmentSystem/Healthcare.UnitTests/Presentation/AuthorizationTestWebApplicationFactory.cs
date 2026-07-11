using Healthcare.Application.Ports.Authentication;
using Healthcare.Adapters.Caching;
using Healthcare.Adapters.Locking;
using Healthcare.Application.Ports.Payments;
using Healthcare.Adapters.Persistence.InMemory;
using Healthcare.Application.Ports.Authentication;
using Healthcare.Application.Ports.Caching;
using Healthcare.Application.Ports.Locking;
using Healthcare.Application.Ports.Payments;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Adapters.Services;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.Services;
using Healthcare.Domain.ValueObjects;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using StackExchange.Redis;

namespace Healthcare.UnitTests.Presentation;

public sealed class AuthorizationTestWebApplicationFactory : WebApplicationFactory<Program>
{
    public AuthorizationTestWebApplicationFactory()
    {
        // Environment variables must be set BEFORE Program.Main runs so that
        // builder.Configuration reads them during AddSecurityServices.
        // Using '__' (double underscore) as the environment-variable key separator.
        SetEnv("Jwt__Secret", "TestSuperSecretKeyForAuthModuleThatIs32Chars!");
        SetEnv("Jwt__Issuer", "HealthcareAPI");
        SetEnv("Jwt__Audience", "HealthcareClients");
        SetEnv("Jwt__ExpirationInMinutes", "60");
        SetEnv("Stripe__SecretKey", "sk_test_mock_auth_tests");
        SetEnv("Stripe__PublishableKey", "pk_test_mock_auth_tests");
        SetEnv("RateLimiting__GlobalPermitLimit", "10000");
        SetEnv("RateLimiting__AuthPermitLimit", "10000");
        SetEnv("RateLimiting__WindowMinutes", "1");
        SetEnv("ConnectionStrings__DefaultConnection", "Server=.;Database=AuthTest_Unused;Trusted_Connection=true;");
        SetEnv("Redis__ConnectionString", "localhost:6379");
        SetEnv("AllowedOrigins__0", "https://example.com");
        SetEnv("Authentication__BreachedPasswordCheckEnabled", "false");
    }

    private static void SetEnv(string key, string value)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            // ── Remove EF Core DbContext ──────────────────────────
            // RemoveAll<DbContextOptions<HealthcareDbContext>> requires the
            // generic type; RemoveAll<DbContextOptions>() does NOT match it.
            for (int i = services.Count - 1; i >= 0; i--)
            {
                var sd = services[i];
                if (sd.ServiceType.Name == "DbContextOptions`1")
                    services.RemoveAt(i);
            }
            services.RemoveAll<Healthcare.Adapters.Persistence.EntityFramework.HealthcareDbContext>();

            // ── Remove infrastructure services that depend on Docker ─
            services.RemoveAll<IAppointmentRepository>();
            services.RemoveAll<IPatientRepository>();
            services.RemoveAll<IDoctorRepository>();
            services.RemoveAll<IUserRepository>();
            services.RemoveAll<IPaymentRepository>();
            services.RemoveAll<IAuditLogRepository>();
            services.RemoveAll<IUserSessionRepository>();
            services.RemoveAll<IUnitOfWork>();

            services.RemoveAll<IBreachedPasswordChecker>();
            var breachedCheckerMock = new Mock<IBreachedPasswordChecker>();
            breachedCheckerMock
                .Setup(x => x.IsPasswordBreachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            services.AddScoped<IBreachedPasswordChecker>(_ => breachedCheckerMock.Object);

            services.RemoveAll<IConnectionMultiplexer>();
            services.RemoveAll<IDistributedLockService>();
            services.RemoveAll<IDoctorCacheService>();
            services.RemoveAll<IAppointmentCodeGenerator>();

            services.RemoveAll<StripeSettings>();
            services.RemoveAll<IPaymentGateway>();

            // ── Register in-memory alternatives ────────────────────
            services.AddSingleton<IAppointmentRepository, InMemoryAppointmentRepository>();
            services.AddSingleton<IPatientRepository, InMemoryPatientRepository>();
            services.AddSingleton<IDoctorRepository, InMemoryDoctorRepository>();
            services.AddSingleton<IUserRepository, InMemoryUserRepository>();
            services.AddSingleton<IPaymentRepository, InMemoryPaymentRepository>();
            services.AddSingleton<IAuditLogRepository, InMemoryAuditLogRepository>();
            services.AddSingleton<IUserSessionRepository, InMemoryUserSessionRepository>();
            services.AddSingleton<IUnitOfWork, InMemoryUnitOfWork>();

            services.AddSingleton<IDistributedLockService, InMemoryLockService>();
            services.AddSingleton<IDoctorCacheService, InMemoryDoctorCacheService>();
            services.AddSingleton<IAppointmentCodeGenerator, AppointmentCodeGenerator>();

            services.AddScoped<IPaymentGateway>(_ => Mock.Of<IPaymentGateway>());

            var redisMock = new Mock<IConnectionMultiplexer>();
            services.AddSingleton<IConnectionMultiplexer>(_ => redisMock.Object);
        });
    }

    private static int _adminSeedCounter;
    private static readonly object _adminSeedLock = new();

    public string SeedTestAdminUsername { get; private set; } = "testadmin";
    public string SeedTestAdminPassword { get; private set; } = "SecurePass123!";

    /// <summary>
    /// Seeds a test admin user directly into the in-memory store, bypassing the public registration endpoint.
    /// Safe to call multiple times — only seeds once.
    /// </summary>
    public void SeedTestAdmin()
    {
        if (_adminSeedCounter > 0) return;
        lock (_adminSeedLock)
        {
            if (_adminSeedCounter > 0) return;
            using var scope = Services.CreateScope();
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            if (userRepo.GetByUsernameAsync(SeedTestAdminUsername).GetAwaiter().GetResult() != null)
            {
                _adminSeedCounter = 1;
                return;
            }

            var email = Email.Create("testadmin@test.com");
            var passwordHash = passwordHasher.HashPassword(SeedTestAdminPassword);
            var admin = User.Create(SeedTestAdminUsername, email, passwordHash, UserRole.Admin);
            userRepo.AddAsync(admin).GetAwaiter().GetResult();
            _adminSeedCounter = 1;
        }
    }

    protected override void Dispose(bool disposing)
    {
        _adminSeedCounter = 0;
        if (disposing)
        {
            // Clean up environment variables to avoid cross-test pollution
            foreach (var key in new[]
            {
                "Jwt__Secret", "Jwt__Issuer", "Jwt__Audience", "Jwt__ExpirationInMinutes",
                "Stripe__SecretKey", "Stripe__PublishableKey",
                "RateLimiting__GlobalPermitLimit", "RateLimiting__AuthPermitLimit", "RateLimiting__WindowMinutes",
                "ConnectionStrings__DefaultConnection", "Redis__ConnectionString",
                "AllowedOrigins__0",
                "Authentication__BreachedPasswordCheckEnabled",
            })
            {
                Environment.SetEnvironmentVariable(key, null);
            }
        }
        base.Dispose(disposing);
    }
}
