using Healthcare.Domain.Entities;
using Healthcare.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Healthcare.Adapters.Persistence.EntityFramework.Configurations;

/// <summary>
/// Entity Framework configuration for Patient entity.
/// </summary>
/// <remarks>
/// Design Pattern: Fluent API Configuration
/// 
/// This configuration:
/// - Maps Patient entity to database table
/// - Converts Value Objects (Email, PhoneNumber, Address) to database columns
/// - Defines indexes for performance
/// - Sets up relationships with other entities
/// 
/// Value Object Mapping Strategy:
/// - Email → stored as string (nvarchar)
/// - PhoneNumber → stored as string (nvarchar)
/// - Address → owned entity (separate columns in same table)
/// - Gender → stored as int (enum underlying type)
/// </remarks>
public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        // Table name
        builder.ToTable("Patients");

        // Primary key
        builder.HasKey(p => p.Id);

        // Properties
        builder.Property(p => p.FirstName)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.LastName)
            .IsRequired()
            .HasMaxLength(50);

        // Value Object: Email (convert to string)
        builder.Property(p => p.Email)
            .HasConversion(
                email => email.Value,                // To database
                value => Email.Create(value))         // From database
            .IsRequired()
            .HasMaxLength(320); // RFC 5321 max length

        // Value Object: PhoneNumber (convert to string)
        builder.Property(p => p.PhoneNumber)
            .HasConversion(
                phone => phone.Value,
                value => PhoneNumber.Create(value))
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(p => p.DateOfBirth)
            .IsRequired()
            .HasColumnType("date");

        // Enum: Gender (stored as int)
        builder.Property(p => p.Gender)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(p => p.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.ModifiedAt)
            .IsRequired(false);

        // Value Object: Address (Owned Entity - same table)
        builder.OwnsOne(p => p.Address, address =>
        {
            address.Property(a => a.Street)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("Address_Street");

            address.Property(a => a.City)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("Address_City");

            address.Property(a => a.State)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("Address_State");

            address.Property(a => a.PostalCode)
                .IsRequired()
                .HasMaxLength(10)
                .HasColumnName("Address_PostalCode");

            address.Property(a => a.Country)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("Address_Country");
        });

        // Indexes for performance
        builder.HasIndex(p => p.Email)
            .IsUnique()
            .HasDatabaseName("IX_Patients_Email");

        builder.HasIndex(p => new { p.LastName, p.FirstName })
            .HasDatabaseName("IX_Patients_Name");

        builder.HasIndex(p => p.IsActive)
            .HasDatabaseName("IX_Patients_IsActive");

        // Ignore domain events (not persisted)
        builder.Ignore(p => p.DomainEvents);
    }
}