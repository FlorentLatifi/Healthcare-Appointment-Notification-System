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

        builder.Property(m => m.MessageId)
            .IsRequired();

        // Idempotent insert: same domain EventId cannot be written twice.
        builder.HasIndex(m => m.MessageId)
            .IsUnique()
            .HasDatabaseName("IX_OutboxMessages_MessageId");

        builder.Property(m => m.EventType).IsRequired().HasMaxLength(1024);
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.OccurredOn).IsRequired();

        builder.Property(m => m.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(OutboxMessageStatus.Pending);

        builder.Property(m => m.ProcessedAt);
        builder.Property(m => m.DeadLetteredAt);
        builder.Property(m => m.NextAttemptAt).IsRequired();
        builder.Property(m => m.Error).HasMaxLength(2048);
        builder.Property(m => m.RetryCount).IsRequired().HasDefaultValue(0);
        builder.Property(m => m.ProcessingStartedAt);

        // Relay claim query: due pending messages ordered by occurrence.
        builder.HasIndex(m => new { m.Status, m.NextAttemptAt, m.OccurredOn })
            .HasDatabaseName("IX_OutboxMessages_Status_NextAttempt")
            .HasFilter("[Status] = 0"); // Pending only

        // Dead-letter ops / dashboards
        builder.HasIndex(m => new { m.Status, m.DeadLetteredAt })
            .HasDatabaseName("IX_OutboxMessages_DeadLetter")
            .HasFilter("[Status] = 3");
    }
}
