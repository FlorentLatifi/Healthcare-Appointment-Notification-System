using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.IntegrationTests.Helpers;

/// <summary>
/// SQLite-friendly <see cref="HealthcareDbContext"/> (RowVersion / TEXT) for identity regression tests.
/// </summary>
public sealed class SqliteCompatibleDbContext : HealthcareDbContext
{
    public SqliteCompatibleDbContext(DbContextOptions<HealthcareDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AuditLogEntry>()
            .Property(a => a.Details)
            .HasColumnType("TEXT");

        modelBuilder.Entity<Appointment>()
            .Property<byte[]>("RowVersion")
            .ValueGeneratedNever()
            .IsConcurrencyToken();

        modelBuilder.Entity<Doctor>()
            .Property<byte[]>("RowVersion")
            .ValueGeneratedNever()
            .IsConcurrencyToken();

        modelBuilder.Entity<Payment>()
            .Property<byte[]>("RowVersion")
            .ValueGeneratedNever()
            .IsConcurrencyToken();
    }
}
