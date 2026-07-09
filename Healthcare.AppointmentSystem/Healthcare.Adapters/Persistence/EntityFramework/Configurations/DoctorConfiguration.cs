using Healthcare.Domain.Entities;
using Healthcare.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Healthcare.Adapters.Persistence.EntityFramework.Configurations;

/// <summary>
/// Entity Framework configuration for Doctor entity.
/// </summary>
public class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        // Table name
        builder.ToTable("Doctors");

        // Primary key
        builder.HasKey(d => d.Id);

        // Properties
        builder.Property(d => d.FirstName)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(d => d.LastName)
            .IsRequired()
            .HasMaxLength(50);

        // Value Object: Email
        builder.Property(d => d.Email)
            .HasConversion(
                email => email.Value,
                value => Email.Create(value))
            .IsRequired()
            .HasMaxLength(320);

        // Value Object: PhoneNumber
        builder.Property(d => d.PhoneNumber)
            .HasConversion(
                phone => phone.Value,
                value => PhoneNumber.Create(value))
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(d => d.LicenseNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(d => d.IsAcceptingPatients)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(d => d.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(d => d.YearsOfExperience)
            .IsRequired();

        builder.Property<byte[]>("RowVersion")
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.Property(d => d.CreatedAt)
            .IsRequired();

        builder.Property(d => d.ModifiedAt)
            .IsRequired(false);

        // Value Object: Money (ConsultationFee) - Owned Entity
        builder.OwnsOne(d => d.ConsultationFee, money =>
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

        // Indexes
        builder.HasIndex(d => d.Email)
            .IsUnique()
            .HasDatabaseName("IX_Doctors_Email");

        builder.HasIndex(d => d.LicenseNumber)
            .IsUnique()
            .HasDatabaseName("IX_Doctors_LicenseNumber");

        builder.HasIndex(d => new { d.IsActive, d.IsAcceptingPatients })
            .HasDatabaseName("IX_Doctors_Active_AcceptingPatients");

        // Ignore domain events
        builder.Ignore(d => d.DomainEvents);

        // Owned Collection: Specialties
        builder.OwnsMany(d => d.SpecialtyEntries, specialty =>
        {
            specialty.WithOwner().HasForeignKey("DoctorId");
            specialty.ToTable("DoctorSpecialties");
            specialty.HasKey("Id");
            specialty.Property(s => s.Specialty).IsRequired().HasConversion<int>();
        });

        // Owned Collection: WeeklySchedule
        builder.OwnsMany(d => d.WeeklySchedule, schedule =>
        {
            schedule.WithOwner().HasForeignKey("DoctorId");
            schedule.ToTable("DoctorWorkingHours");
            schedule.HasKey("Id");
            schedule.Property(s => s.DayOfWeek).IsRequired();
            schedule.Property(s => s.StartTime).IsRequired(false);
            schedule.Property(s => s.EndTime).IsRequired(false);
            schedule.Property(s => s.IsWorkingDay).IsRequired();
        });
    }
}