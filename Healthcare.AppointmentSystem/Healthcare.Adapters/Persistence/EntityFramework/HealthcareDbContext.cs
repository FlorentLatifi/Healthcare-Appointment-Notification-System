using Healthcare.Adapters.Common;
using Healthcare.Adapters.Events;
using Healthcare.Adapters.Notifications;
using Healthcare.Adapters.Persistence.EntityFramework.Repositories;
using Healthcare.Application.Ports.Common;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Notifications;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Adapters.Persistence.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.Adapters.Persistence.EntityFramework;

/// <summary>
/// Entity Framework DbContext for Healthcare system.
/// </summary>
/// <remarks>
/// Design Pattern: Unit of Work (EF Core implements this pattern)
/// 
/// This DbContext:
/// - Manages database connection
/// - Tracks entity changes
/// - Coordinates transaction lifecycle
/// - Maps domain entities to database tables
/// 
/// Configuration Strategy:
/// - Uses Fluent API for entity configuration (separate configuration classes)
/// - Maintains clean separation between Domain and Infrastructure
/// - Converts Value Objects to database primitives
/// </remarks>
public class HealthcareDbContext : DbContext
{
    public HealthcareDbContext(DbContextOptions<HealthcareDbContext> options)
        : base(options)
    {
    }

    // DbSets for entities
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Doctor> Doctors => Set<Doctor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HealthcareDbContext).Assembly);
    }

    /// <summary>
    /// Registers adapters with Entity Framework Core persistence.
    /// </summary>
    /// <remarks>
    /// Use this for production with SQL Server database.
    /// 
    /// Configuration Required:
    /// - Connection string in appsettings.json
    /// - SQL Server instance running
    /// - Database created (via migrations)
    /// 
    /// Usage in Program.cs:
    /// builder.Services.AddAdaptersWithEFCorePersistence(
    ///     builder.Configuration.GetConnectionString("DefaultConnection"));
    /// </remarks>
    public static IServiceCollection AddAdaptersWithEFCorePersistence(
        this IServiceCollection services,
        string connectionString)
    {
        // Database Context
        services.AddDbContext<HealthcareDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Repositories (EF Core implementations)
        services.AddScoped<IAppointmentRepository, EFCoreAppointmentRepository>();
        services.AddScoped<IPatientRepository, EFCorePatientRepository>();
        services.AddScoped<IDoctorRepository, EFCoreDoctorRepository>();
        services.AddScoped<IUnitOfWork, EFCoreUnitOfWork>();

        // Notification Service (Console for now)
        services.AddScoped<INotificationService, ConsoleNotificationAdapter>();

        // Event Infrastructure
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        RegisterEventHandlers(services);

        // Time Provider
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();

        return services;
    }
}