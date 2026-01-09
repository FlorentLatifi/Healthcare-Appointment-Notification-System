using Healthcare.Domain.Entities;
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
    public DbSet<User> Users => Set<User>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HealthcareDbContext).Assembly);
    }
}