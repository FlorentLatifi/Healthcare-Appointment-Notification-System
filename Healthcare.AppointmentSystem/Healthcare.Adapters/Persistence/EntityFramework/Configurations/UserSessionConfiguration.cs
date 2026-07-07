using Healthcare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Healthcare.Adapters.Persistence.EntityFramework.Configurations;

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.UserId).IsRequired();
        builder.Property(s => s.FamilyId).IsRequired();
        builder.Property(s => s.LastUsedAt).IsRequired();
        builder.Property(s => s.UserAgent).HasMaxLength(500).IsRequired(false);
        builder.Property(s => s.IpAddress).HasMaxLength(45).IsRequired(false);
        builder.Property(s => s.RevokedAt).IsRequired(false);
        builder.Property(s => s.CreatedAt).IsRequired();

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.UserId, s.FamilyId })
            .HasDatabaseName("IX_UserSessions_UserId_FamilyId");

        builder.Ignore(s => s.DomainEvents);
    }
}
