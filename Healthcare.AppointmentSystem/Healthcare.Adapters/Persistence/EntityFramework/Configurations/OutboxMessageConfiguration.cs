using Healthcare.Adapters.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Healthcare.Adapters.Persistence.EntityFramework.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.EventType).IsRequired().HasMaxLength(1024);
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.OccurredOn).IsRequired();
        builder.Property(m => m.ProcessedAt);
        builder.Property(m => m.Error).HasMaxLength(2048);
        builder.Property(m => m.RetryCount).IsRequired().HasDefaultValue(0);

        // Relay: WHERE ProcessedAt IS NULL AND RetryCount < N ORDER BY OccurredOn
        // Filtered index avoids scanning historical processed rows.
        builder.HasIndex(m => new { m.OccurredOn, m.RetryCount })
            .HasDatabaseName("IX_OutboxMessages_Pending")
            .HasFilter("[ProcessedAt] IS NULL");
    }
}
