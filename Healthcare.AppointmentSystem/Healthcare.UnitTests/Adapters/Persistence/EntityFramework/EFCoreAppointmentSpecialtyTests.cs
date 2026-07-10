using FluentAssertions;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Adapters.Persistence.EntityFramework.Repositories;
using Healthcare.Adapters.Services;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Healthcare.UnitTests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.UnitTests.Adapters.Persistence.EntityFramework;

[Trait("Category", "Integration")]
public sealed class EFCoreAppointmentSpecialtyTests
{
    [Fact]
    public async Task GetByIdAsync_IncludesDoctorSpecialties()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlite(connection)
            .Options;

        var doctor = TestDataBuilder.ADoctor()
            .WithEmail("spec.appt.doctor@test.com")
            .WithLicense("LIC-SPEC-APPT-001")
            .WithSpecialty(Specialty.GeneralPractice)
            .Build();
        doctor.AddSpecialty(Specialty.Cardiology);
        doctor.AddSpecialty(Specialty.Pediatrics);

        var patient = TestDataBuilder.APatient()
            .WithEmail("spec.appt.patient@test.com")
            .Build();

        int appointmentId;
        await using (var seedCtx = new SqliteCompatibleDbContext(options))
        {
            await seedCtx.Database.EnsureCreatedAsync();
            seedCtx.Doctors.Add(doctor);
            seedCtx.Patients.Add(patient);
            await seedCtx.SaveChangesAsync();

            var appointmentTime = AppointmentTime.Create(
                DateTime.UtcNow.Date.AddDays(10).AddHours(10));
            var appointment = Appointment.Create(
                patient, doctor, appointmentTime, "Specialty checkup test",
                AppointmentCodeGenerator.Instance);
            appointment.ApplyPricingStrategy(
                doctor.ConsultationFee.Amount, doctor.ConsultationFee.Currency);
            seedCtx.Appointments.Add(appointment);
            await seedCtx.SaveChangesAsync();
            appointmentId = appointment.Id;
        }

        await using var context = new SqliteCompatibleDbContext(options);
        var repository = new EFCoreAppointmentRepository(context);

        var loaded = await repository.GetByIdAsync(appointmentId);

        loaded.Should().NotBeNull();
        loaded!.Doctor.Should().NotBeNull();
        loaded.Doctor.Specialties.Should().HaveCount(3, because: "the doctor has 3 specialties");
        loaded.Doctor.Specialties.Should().Contain(Specialty.GeneralPractice);
        loaded.Doctor.Specialties.Should().Contain(Specialty.Cardiology);
        loaded.Doctor.Specialties.Should().Contain(Specialty.Pediatrics);
    }
}
