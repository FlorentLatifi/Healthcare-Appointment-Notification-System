using Healthcare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Healthcare.Adapters.Persistence.EntityFramework.Configurations;

public sealed class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("UserNotifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.UserId).IsRequired();
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(2000).IsRequired();
        builder.Property(n => n.IsRead).IsRequired();
        builder.Property(n => n.Category).HasMaxLength(50).IsRequired(false);
        builder.Property(n => n.RelatedEntityType).HasMaxLength(50).IsRequired(false);
        builder.Property(n => n.RelatedEntityId).IsRequired(false);
        builder.Property(n => n.ReadAt).IsRequired(false);
        builder.Property(n => n.CreatedAt).IsRequired();
        builder.Property(n => n.ModifiedAt).IsRequired(false);

        builder.HasIndex(n => new { n.UserId, n.CreatedAt })
            .HasDatabaseName("IX_UserNotifications_User_Created");

        // No SQL Server-only filtered index: SQLite EnsureCreated (unit tests) rejects
        // filter syntax / provider-specific DDL. Composite index is enough for inbox queries.
        builder.HasIndex(n => new { n.UserId, n.IsRead })
            .HasDatabaseName("IX_UserNotifications_User_Unread");

        builder.Ignore(n => n.DomainEvents);
    }
}
