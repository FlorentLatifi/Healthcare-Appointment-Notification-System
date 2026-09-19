using Healthcare.Adapters.Events;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.UnitTests.Helpers;

/// <summary>
/// SQLite-friendly model tweaks for unit tests that call EnsureCreated.
/// SQL Server types (nvarchar(max), rowversion, filtered indexes) break SQLite DDL.
/// </summary>
public sealed class SqliteCompatibleDbContext : HealthcareDbContext
{
    public SqliteCompatibleDbContext(DbContextOptions<HealthcareDbContext> options)
        : base(options)
    {
    }

    public SqliteCompatibleDbContext(DbContextOptions<HealthcareDbContext> options, OutboxSettings outboxSettings)
        : base(options, outboxSettings)
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

        // Drop SQL Server-only index filters for SQLite EnsureCreated.
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var index in entity.GetIndexes())
            {
                if (index.GetFilter() is not null)
                    index.SetFilter(null);
            }
        }
    }
}
