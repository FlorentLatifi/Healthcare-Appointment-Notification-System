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
        builder.HasIndex(m => m.ProcessedAt);
    }
}
