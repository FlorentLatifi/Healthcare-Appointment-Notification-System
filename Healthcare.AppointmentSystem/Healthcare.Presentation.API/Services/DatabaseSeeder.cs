using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Application.Ports.Authentication;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.Presentation.API.Services;

public sealed class DatabaseSeeder : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(IServiceScopeFactory scopeFactory, ILogger<DatabaseSeeder> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Database seeder started");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<HealthcareDbContext>();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

            _logger.LogInformation("Applying pending migrations...");
            await dbContext.Database.MigrateAsync(cancellationToken);
            _logger.LogInformation("Migrations applied successfully.");

            if (await dbContext.Doctors.AnyAsync(cancellationToken))
            {
                _logger.LogInformation("Doctors table already has data — skipping seed.");
                return;
            }

            _logger.LogInformation("Seeding demo data...");

            await SeedAdminUser(authService, cancellationToken);

            await SeedDoctors(dbContext, cancellationToken);

            _logger.LogInformation("Demo data seeded successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed demo data.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task SeedAdminUser(IAuthenticationService authService, CancellationToken ct)
    {
        var result = await authService.RegisterAsync(
            "admin", "admin@healthcareclinic.com", "Admin123!", "Admin", ct);

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Failed to create admin user: {result.Error}");
        }
    }

    private static async Task SeedDoctors(HealthcareDbContext dbContext, CancellationToken ct)
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
