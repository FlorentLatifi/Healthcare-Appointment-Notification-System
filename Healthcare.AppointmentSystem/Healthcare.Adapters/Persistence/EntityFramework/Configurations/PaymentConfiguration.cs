using Healthcare.Domain.Entities;
using Healthcare.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Healthcare.Adapters.Persistence.EntityFramework.Configurations;

/// <summary>
/// Entity Framework configuration for Payment entity.
/// </summary>
public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        // Table name
        builder.ToTable("Payments");

        // Primary key
        builder.HasKey(p => p.Id);

        // Foreign key to Appointment
        builder.HasOne(p => p.Appointment)
            .WithMany()
            .HasForeignKey(p => p.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // Value Object: Money (Amount)
        builder.OwnsOne(p => p.Amount, money =>
        {
            money.Property(m => m.Amount)
                .IsRequired()
                .HasColumnType("decimal(18,2)")
                .HasColumnName("Amount");

            money.Property(m => m.Currency)
                .IsRequired()
                .HasMaxLength(3)
                .HasColumnName("Currency");
        });

        // Enum: PaymentStatus
        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<int>();

        // Value Object: TransactionId (nullable)
        builder.Property(p => p.TransactionId)
            .HasConversion(
                tid => tid != null ? tid.Value : null,
                value => value != null ? TransactionId.Create(value) : null)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(p => p.PaymentMethod)
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(p => p.PaymentProcessor)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.PaidAt)
            .IsRequired(false);

        builder.Property(p => p.RefundedAt)
            .IsRequired(false);

        // Value Object: RefundTransactionId (nullable)
        builder.Property(p => p.RefundTransactionId)
            .HasConversion(
                tid => tid != null ? tid.Value : null,
                value => value != null ? TransactionId.Create(value) : null)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(p => p.FailureReason)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property<byte[]>("RowVersion")
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.ModifiedAt)
            .IsRequired(false);

        // Indexes
        builder.HasIndex(p => p.AppointmentId)
            .IsUnique()
            .HasDatabaseName("IX_Payments_AppointmentId");

        // Stripe / gateway lookups (unique when present)
        builder.HasIndex(p => p.TransactionId)
            .IsUnique()
            .HasDatabaseName("IX_Payments_TransactionId")
            .HasFilter("[TransactionId] IS NOT NULL");

        // Revenue reports: WHERE Status = Succeeded AND PaidAt >= @from AND PaidAt < @to
        builder.HasIndex(p => new { p.Status, p.PaidAt })
            .HasDatabaseName("IX_Payments_Status_PaidAt");

        // Ignore domain events
        builder.Ignore(p => p.DomainEvents);
    }
}