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
        builder.Property(a => a.Details).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(a => a.UserId).IsRequired(false);
        builder.Property(a => a.ActorRole).HasMaxLength(50).IsRequired(false);
        builder.Property(a => a.Outcome).IsRequired().HasMaxLength(20).HasDefaultValue("Success");
        builder.Property(a => a.ClientIp).HasMaxLength(64).IsRequired(false);
        builder.Property(a => a.CorrelationId).HasMaxLength(64).IsRequired(false);
        builder.Property(a => a.UserAgent).HasMaxLength(512).IsRequired(false);
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.ModifiedAt).IsRequired(false);

        // Not mapped convenience aliases
        builder.Ignore(a => a.Action);
        builder.Ignore(a => a.ResourceType);
        builder.Ignore(a => a.ResourceId);
        builder.Ignore(a => a.ActorUserId);
        builder.Ignore(a => a.DomainEvents);

        builder.HasIndex(a => a.EventType).HasDatabaseName("IX_AuditLogs_EventType");
        builder.HasIndex(a => new { a.EntityType, a.EntityId, a.OccurredOn })
            .HasDatabaseName("IX_AuditLogs_Entity_Time");
        builder.HasIndex(a => a.OccurredOn).HasDatabaseName("IX_AuditLogs_OccurredOn");
        builder.HasIndex(a => a.UserId).HasDatabaseName("IX_AuditLogs_UserId");
        builder.HasIndex(a => a.CorrelationId).HasDatabaseName("IX_AuditLogs_CorrelationId");
        builder.HasIndex(a => a.Outcome).HasDatabaseName("IX_AuditLogs_Outcome");
    }
}
