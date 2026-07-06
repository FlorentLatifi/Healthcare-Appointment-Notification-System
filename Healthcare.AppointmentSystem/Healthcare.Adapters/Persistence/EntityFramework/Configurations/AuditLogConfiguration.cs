using Healthcare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Healthcare.Adapters.Persistence.EntityFramework.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.EventType).IsRequired().HasMaxLength(100);
        builder.Property(a => a.EntityType).IsRequired().HasMaxLength(100);
        builder.Property(a => a.EntityId).IsRequired(false);
        builder.Property(a => a.OccurredOn).IsRequired();
        builder.Property(a => a.Details).IsRequired();
        builder.Property(a => a.UserId).IsRequired(false);
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.ModifiedAt).IsRequired(false);

        builder.HasIndex(a => a.EventType).HasDatabaseName("IX_AuditLogs_EventType");
        builder.HasIndex(a => a.EntityType).HasDatabaseName("IX_AuditLogs_EntityType");
        builder.HasIndex(a => new { a.EntityType, a.EntityId }).HasDatabaseName("IX_AuditLogs_Entity");
        builder.HasIndex(a => a.OccurredOn).HasDatabaseName("IX_AuditLogs_OccurredOn");
        builder.Ignore(a => a.DomainEvents);
    }
}
