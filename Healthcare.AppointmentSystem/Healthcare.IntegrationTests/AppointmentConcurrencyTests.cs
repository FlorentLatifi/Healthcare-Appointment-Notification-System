using FluentAssertions;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Adapters.Services;
using Healthcare.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.IntegrationTests;

public sealed class AppointmentConcurrencyTests : IClassFixture<SqlServerTestFixture>
{
    private readonly SqlServerTestFixture _fixture;

    public AppointmentConcurrencyTests(SqlServerTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveChangesAsync_WithConcurrentUpdates_ThrowsDbUpdateConcurrencyException()
    {
        var options = _fixture.CreateDbContextOptions();

        var doctor = Doctor.Create(
            "Concurrency", "Doctor",
            Email.Create("concurrency.doctor@test.com"),
            PhoneNumber.Create("+355672345678"),
            "CON-10001",
            Money.Create(100m, "USD"),
            10,
            Specialty.Cardiology);

        var patient = Patient.Create(
            "Concurrency", "Patient",
            Email.Create("concurrency.patient@test.com"),
            PhoneNumber.Create("+355672345678"),
            new DateTime(1990, 1, 1),
            Gender.Male,
            Address.Create("1 Test St", "Pristina", "Kosovo", "10000", "Kosovo"));

        var appointmentTime = AppointmentTime.Create(
            DateTime.UtcNow.Date.AddDays(30).AddHours(10));

        int appointmentId;
        await using (var seedCtx = new HealthcareDbContext(options))
        {
            seedCtx.Doctors.Add(doctor);
            seedCtx.Patients.Add(patient);
            await seedCtx.SaveChangesAsync();

            var appointment = Appointment.Create(patient, doctor, appointmentTime,
                "Concurrency test appointment for optimistic locking verification",
                AppointmentCodeGenerator.Instance);
            seedCtx.Appointments.Add(appointment);
            await seedCtx.SaveChangesAsync();
            appointmentId = appointment.Id;
        }

        await using var ctx1 = new HealthcareDbContext(options);
        await using var ctx2 = new HealthcareDbContext(options);

        var a1 = await ctx1.Appointments.FirstAsync(a => a.Id == appointmentId);
        var a2 = await ctx2.Appointments.FirstAsync(a => a.Id == appointmentId);

        a1.ApplyPricingStrategy(75, "USD");
        await ctx1.SaveChangesAsync();

        a2.ApplyPricingStrategy(80, "USD");
        var act = () => ctx2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
