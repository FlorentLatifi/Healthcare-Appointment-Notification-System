using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.UnitTests.Helpers;

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
    }
}
