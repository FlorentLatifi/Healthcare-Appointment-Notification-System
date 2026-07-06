using FluentAssertions;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Adapters.Persistence.EntityFramework.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.Services;
using Healthcare.Domain.ValueObjects;
using Healthcare.UnitTests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.UnitTests.Adapters.Persistence.EntityFramework;

public sealed class EFCorePaginationTests
{
    [Fact]
    public async Task GetPagedAsync_Appointments_ReturnsCorrectPage()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlite(connection)
            .Options;

        var doctor = TestDataBuilder.ADoctor()
            .WithEmail("paging.doctor@test.com")
            .WithLicense("LIC-PAGING-01")
            .Build();
        var patient = TestDataBuilder.APatient()
            .WithEmail("paging.patient@test.com")
            .Build();

        await using (var seedCtx = new SqliteCompatibleDbContext(options))
        {
            await seedCtx.Database.EnsureCreatedAsync();
            seedCtx.Doctors.Add(doctor);
            seedCtx.Patients.Add(patient);
            await seedCtx.SaveChangesAsync();

            for (int i = 0; i < 25; i++)
            {
                var appointmentTime = AppointmentTime.Create(
                    DateTime.UtcNow.Date.AddDays(i + 30).AddHours(10));
                var appointment = Appointment.Create(
                    patient, doctor, appointmentTime, $"Checkup #{i}",
                    AppointmentCodeGenerator.Instance);
                appointment.ApplyPricingStrategy(
                    doctor.ConsultationFee.Amount, doctor.ConsultationFee.Currency);
                seedCtx.Appointments.Add(appointment);
            }
            await seedCtx.SaveChangesAsync();
        }

        await using var context = new SqliteCompatibleDbContext(options);
        var repository = new EFCoreAppointmentRepository(context);

        var page1 = await repository.GetPagedAsync(1, 10);
        page1.Items.Should().HaveCount(10);
        page1.TotalCount.Should().Be(25);
        page1.PageNumber.Should().Be(1);

        var page2 = await repository.GetPagedAsync(2, 10);
        page2.Items.Should().HaveCount(10);
        page2.TotalCount.Should().Be(25);

        var page3 = await repository.GetPagedAsync(3, 10);
        page3.Items.Should().HaveCount(5);
        page3.TotalCount.Should().Be(25);
    }

    [Fact]
    public async Task GetPagedAsync_Appointments_EmptyCollection_ReturnsEmpty()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new SqliteCompatibleDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var repository = new EFCoreAppointmentRepository(context);
        var result = await repository.GetPagedAsync(1, 10);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_Patients_ReturnsCorrectPage()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var seedCtx = new SqliteCompatibleDbContext(options))
        {
            await seedCtx.Database.EnsureCreatedAsync();
            for (int i = 0; i < 12; i++)
            {
                var patient = TestDataBuilder.APatient()
                    .WithEmail($"paging.patient{i}@test.com")
                    .Build();
                seedCtx.Patients.Add(patient);
            }
            await seedCtx.SaveChangesAsync();
        }

        await using var context = new SqliteCompatibleDbContext(options);
        var repository = new EFCorePatientRepository(context);

        var page1 = await repository.GetPagedAsync(1, 10);
        page1.Items.Should().HaveCount(10);
        page1.TotalCount.Should().Be(12);

        var page2 = await repository.GetPagedAsync(2, 10);
        page2.Items.Should().HaveCount(2);
        page2.TotalCount.Should().Be(12);

        var page3 = await repository.GetPagedAsync(3, 10);
        page3.Items.Should().BeEmpty();
        page3.TotalCount.Should().Be(12);
    }

    [Fact]
    public async Task GetPagedAsync_Payments_ReturnsCorrectPage()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlite(connection)
            .Options;

        var doctor = TestDataBuilder.ADoctor()
            .WithEmail("payment.paging.doctor@test.com")
            .WithLicense("LIC-PAY-PG-01")
            .Build();
        var patient = TestDataBuilder.APatient()
            .WithEmail("payment.paging.patient@test.com")
            .Build();

        int appointmentId;
        await using (var seedCtx = new SqliteCompatibleDbContext(options))
        {
            await seedCtx.Database.EnsureCreatedAsync();
            seedCtx.Doctors.Add(doctor);
            seedCtx.Patients.Add(patient);
            await seedCtx.SaveChangesAsync();

            var appointmentTime = AppointmentTime.Create(
                DateTime.UtcNow.Date.AddDays(30).AddHours(10));
            var appointment = Appointment.Create(
                patient, doctor, appointmentTime, "Payment paging test",
                AppointmentCodeGenerator.Instance);
            seedCtx.Appointments.Add(appointment);
            await seedCtx.SaveChangesAsync();
            appointmentId = appointment.Id;

            for (int i = 0; i < 20; i++)
            {
                var payment = Payment.Create(appointmentId, Money.Create(100, "USD"));
                seedCtx.Payments.Add(payment);
            }
            await seedCtx.SaveChangesAsync();
        }

        await using var context = new SqliteCompatibleDbContext(options);
        var repository = new EFCorePaymentRepository(context);

        var page1 = await repository.GetPagedAsync(1, 15);
        page1.Items.Should().HaveCount(15);
        page1.TotalCount.Should().Be(20);

        var page2 = await repository.GetPagedAsync(2, 15);
        page2.Items.Should().HaveCount(5);
        page2.TotalCount.Should().Be(20);
    }

    [Fact]
    public async Task GetPagedActiveAsync_Patients_ReturnsOnlyActivePatients()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var seedCtx = new SqliteCompatibleDbContext(options))
        {
            await seedCtx.Database.EnsureCreatedAsync();
            for (int i = 0; i < 8; i++)
            {
                var patient = TestDataBuilder.APatient()
                    .WithEmail($"active{i}@test.com")
                    .Build();
                seedCtx.Patients.Add(patient);
            }
            for (int i = 0; i < 3; i++)
            {
                var patient = TestDataBuilder.APatient()
                    .WithEmail($"inactive{i}@test.com")
                    .Build();
                patient.Deactivate();
                seedCtx.Patients.Add(patient);
            }
            await seedCtx.SaveChangesAsync();
        }

        await using var context = new SqliteCompatibleDbContext(options);
        var repository = new EFCorePatientRepository(context);

        var result = await repository.GetPagedActiveAsync(1, 5);
        result.Items.Should().HaveCount(5);
        result.TotalCount.Should().Be(8);
        result.Items.All(p => p.IsActive).Should().BeTrue();

        var page2 = await repository.GetPagedActiveAsync(2, 5);
        page2.Items.Should().HaveCount(3);
        page2.TotalCount.Should().Be(8);
    }

    [Fact]
    public async Task GetPagedSearchByNameAsync_Patients_ReturnsMatchingPatients()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var seedCtx = new SqliteCompatibleDbContext(options))
        {
            await seedCtx.Database.EnsureCreatedAsync();
            foreach (var name in new[] { "Alice", "Bob", "Charlie", "David", "Eve" })
            {
                var patient = TestDataBuilder.APatient()
                    .WithName(name, "Smith")
                    .WithEmail($"{name.ToLower()}.smith@test.com")
                    .Build();
                seedCtx.Patients.Add(patient);
            }
            await seedCtx.SaveChangesAsync();
        }

        await using var context = new SqliteCompatibleDbContext(options);
        var repository = new EFCorePatientRepository(context);

        var result = await repository.GetPagedSearchByNameAsync("Smith", 1, 2);
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(5);

        var page2 = await repository.GetPagedSearchByNameAsync("Smith", 3, 2);
        page2.Items.Should().HaveCount(1);
        page2.TotalCount.Should().Be(5);
    }
}
