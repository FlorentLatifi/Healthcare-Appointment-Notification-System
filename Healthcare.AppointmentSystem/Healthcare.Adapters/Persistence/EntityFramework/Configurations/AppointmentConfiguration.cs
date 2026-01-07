using Healthcare.Domain.Entities;
using Healthcare.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Org.BouncyCastle.Asn1;

namespace Healthcare.Adapters.Persistence.EntityFramework.Configurations;

/// <summary>
/// Entity Framework configuration for Appointment entity.
/// </summary>
public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        // Table name
        builder.ToTable("Appointments");

        // Primary key
        builder.HasKey(a => a.Id);

        // Foreign Keys & Relationships
        builder.HasOne(a => a.Patient)
            .WithMany()
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict); // Prevent cascading delete

        builder.HasOne(a => a.Doctor)
            .WithMany()
            .HasForeignKey(a => a.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Value Object: AppointmentTime
        builder.Property(a => a.ScheduledTime)
            .HasConversion(
                time => time.Value,
                value => AppointmentTime.Create(value))
            .IsRequired()
            .HasColumnName("ScheduledTime");

        // Enum: Status (stored as int)
        builder.Property(a => a.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(a => a.Reason)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.DoctorNotes)
            .HasMaxLength(2000);

        builder.Property(a => a.CancellationReason)
            .HasMaxLength(500);

        builder.Property(a => a.ConfirmedAt)
            .IsRequired(false);

        builder.Property(a => a.CompletedAt)
            .IsRequired(false);

        builder.Property(a => a.CancelledAt)
            .IsRequired(false);

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.Property(a => a.ModifiedAt)
            .IsRequired(false);

        // Value Object: Money (ConsultationFee)
        builder.OwnsOne(a => a.ConsultationFee, money =>
        {
            money.Property(m => m.Amount)
                .IsRequired()
                .HasColumnType("decimal(18,2)")
                .HasColumnName("ConsultationFee_Amount");

            money.Property(m => m.Currency)
                .IsRequired()
                .HasMaxLength(3)
                .HasColumnName("ConsultationFee_Currency");
        });

        // Indexes for queries
        builder.HasIndex(a => a.PatientId)
            .HasDatabaseName("IX_Appointments_PatientId");

        builder.HasIndex(a => a.DoctorId)
            .HasDatabaseName("IX_Appointments_DoctorId");

        builder.HasIndex(a => new { a.DoctorId, a.ScheduledTime })
            .HasDatabaseName("IX_Appointments_Doctor_Time");

        builder.HasIndex(a => a.Status)
            .HasDatabaseName("IX_Appointments_Status");

        builder.HasIndex(a => new { a.Status, a.ScheduledTime })
            .HasDatabaseName("IX_Appointments_Status_Time");

        // Ignore domain events
        builder.Ignore(a => a.DomainEvents);
    }
}
