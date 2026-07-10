using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Application.Ports.Authentication;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Healthcare.Presentation.API.Services;

/// <summary>
/// Applies EF migrations, optionally bootstraps the first Admin from secrets,
/// and optionally seeds demo doctors under strict environment policy.
/// </summary>
/// <remarks>
/// Never creates predictable credentials. Demo data is blocked in Production.
/// Admin bootstrap requires a strong password from configuration/secrets
/// (or a one-time generated password in non-Production only).
/// </remarks>
public sealed class DatabaseSeeder : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _environment;
    private readonly SeedingOptions _options;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        IServiceScopeFactory scopeFactory,
        IHostEnvironment environment,
        IOptions<SeedingOptions> options,
        ILogger<DatabaseSeeder> logger)
    {
        _scopeFactory = scopeFactory;
        _environment = environment;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Database initializer started (Environment={Environment})",
            _environment.EnvironmentName);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<HealthcareDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            _logger.LogInformation("Applying pending migrations...");
            await dbContext.Database.MigrateAsync(cancellationToken);
            _logger.LogInformation("Migrations applied successfully.");

            await BootstrapAdminIfNeededAsync(dbContext, passwordHasher, cancellationToken);
            await SeedDemoDataIfAllowedAsync(dbContext, cancellationToken);
        }
        catch (Exception ex)
        {
            // Fail closed for bootstrap password/policy errors in Production so the process
            // does not continue half-configured. Demo seed failures are still logged.
            if (SeedingPolicy.IsProduction(_environment.EnvironmentName) &&
                _options.BootstrapAdmin.Enabled)
            {
                _logger.LogCritical(ex, "Database initialization failed in Production.");
                throw;
            }

            _logger.LogError(ex, "Database initialization failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task BootstrapAdminIfNeededAsync(
        HealthcareDbContext dbContext,
        IPasswordHasher passwordHasher,
        CancellationToken ct)
    {
        var bootstrap = _options.BootstrapAdmin;
        if (!bootstrap.Enabled)
        {
            _logger.LogInformation("Admin bootstrap is disabled (Seeding:BootstrapAdmin:Enabled=false).");
            return;
        }

        var adminExists = await dbContext.Users
            .AnyAsync(u => u.Role == UserRole.Admin, ct);

        if (adminExists)
        {
            _logger.LogInformation("An Admin user already exists — skipping admin bootstrap.");
            return;
        }

        var username = string.IsNullOrWhiteSpace(bootstrap.Username)
            ? "admin"
            : bootstrap.Username.Trim();

        if (string.IsNullOrWhiteSpace(bootstrap.Email))
        {
            throw new InvalidOperationException(
                "Seeding:BootstrapAdmin:Email is required when bootstrap is enabled.");
        }

        var password = bootstrap.Password;
        var passwordWasGenerated = false;

        if (string.IsNullOrWhiteSpace(password))
        {
            if (!SeedingPolicy.CanGenerateBootstrapPassword(_environment.EnvironmentName))
            {
                throw new InvalidOperationException(
                    "Seeding:BootstrapAdmin:Password is required in Production. " +
                    "Provide it via environment variable Seeding__BootstrapAdmin__Password " +
                    "(or a secret store). Hardcoded/default passwords are not used.");
            }

            password = SeedingPolicy.GenerateSecurePassword();
            passwordWasGenerated = true;
        }

        SeedingPolicy.EnsureStrongPassword(password);

        // Username uniqueness (admin role may be free but username taken)
        var usernameTaken = await dbContext.Users
            .AnyAsync(u => u.Username == username, ct);
        if (usernameTaken)
        {
            throw new InvalidOperationException(
                $"Cannot bootstrap admin: username '{username}' is already taken by a non-admin user.");
        }

        var email = Email.Create(bootstrap.Email.Trim());
        var passwordHash = passwordHasher.HashPassword(password);
        var admin = User.Create(username, email, passwordHash, UserRole.Admin);

        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync(ct);

        if (passwordWasGenerated)
        {
            // One-time credential disclosure for local/dev only — never log passwords from config/secrets.
            _logger.LogWarning(
                "BOOTSTRAP ADMIN CREATED with a generated password (non-Production only). " +
                "Username={Username}; Email={Email}; GeneratedPassword={Password}. " +
                "Store this password securely and change it after first login. " +
                "This message will not appear again after an Admin exists.",
                username,
                email.Value,
                password);
        }
        else
        {
            _logger.LogInformation(
                "Bootstrap Admin created. Username={Username}; Email={Email}. " +
                "Password was supplied via configuration/secrets and is not logged.",
                username,
                email.Value);
        }
    }

    private async Task SeedDemoDataIfAllowedAsync(
        HealthcareDbContext dbContext,
        CancellationToken ct)
    {
        if (!SeedingPolicy.CanSeedDemoData(_environment.EnvironmentName, _options))
        {
            _logger.LogInformation(
                "Skipping demo data seed: {Reason}",
                SeedingPolicy.DescribeDemoBlockReason(_environment.EnvironmentName, _options));
            return;
        }

        if (await dbContext.Doctors.AnyAsync(ct))
        {
            _logger.LogInformation("Doctors table already has data — skipping demo doctor seed.");
            return;
        }

        _logger.LogInformation(
            "Seeding demo doctors (Environment={Environment})...",
            _environment.EnvironmentName);

        await SeedDoctorsAsync(dbContext, ct);

        _logger.LogInformation("Demo doctor data seeded successfully.");
    }

    private static async Task SeedDoctorsAsync(HealthcareDbContext dbContext, CancellationToken ct)
    {
        var doctors = new List<Doctor>
        {
            Doctor.Create(
                "Sarah", "Chen",
                Email.Create("sarah.chen@healthcareclinic.com"),
                PhoneNumber.Create("+12025551234"),
                "MED-001", Money.Create(150.00m, "USD"), 12,
                Specialty.GeneralPractice),
            Doctor.Create(
                "Marcus", "Johnson",
                Email.Create("marcus.johnson@healthcareclinic.com"),
                PhoneNumber.Create("+12025551235"),
                "MED-002", Money.Create(200.00m, "USD"), 8,
                Specialty.Cardiology),
            Doctor.Create(
                "Emily", "Rodriguez",
                Email.Create("emily.rodriguez@healthcareclinic.com"),
                PhoneNumber.Create("+12025551236"),
                "MED-003", Money.Create(180.00m, "USD"), 6,
                Specialty.Pediatrics),
            Doctor.Create(
                "James", "Kim",
                Email.Create("james.kim@healthcareclinic.com"),
                PhoneNumber.Create("+12025551237"),
                "MED-004", Money.Create(250.00m, "USD"), 15,
                Specialty.Neurology),
        };

        dbContext.Doctors.AddRange(doctors);
        await dbContext.SaveChangesAsync(ct);
    }
}
