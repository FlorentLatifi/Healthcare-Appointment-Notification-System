using FluentAssertions;
using Healthcare.Adapters.Persistence.InMemory;
using Healthcare.Application.Common;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.Services;
using Healthcare.Domain.ValueObjects;
using Xunit;

namespace Healthcare.UnitTests.Adapters.Persistence;

public sealed class InMemoryPaginationTests
{
    private readonly IAppointmentRepository _appointmentRepo;
    private readonly IPatientRepository _patientRepo;
    private readonly IPaymentRepository _paymentRepo;

    public InMemoryPaginationTests()
    {
        _appointmentRepo = new InMemoryAppointmentRepository();
        _patientRepo = new InMemoryPatientRepository();
        _paymentRepo = new InMemoryPaymentRepository();
    }

    [Fact]
    public async Task GetPagedAsync_Appointments_ReturnsCorrectPage()
    {
        for (int i = 0; i < 25; i++)
        {
            var patient = CreatePatient($"patient{i}@test.com");
            var doctor = CreateDoctor();
            var appointmentTime = AppointmentTime.Create(
                DateTime.UtcNow.Date.AddDays(i + 30).AddHours(10));
            var appointment = Appointment.Create(
                patient, doctor, appointmentTime, $"Medical checkup #{i}",
                AppointmentCodeGenerator.Instance);
            await _appointmentRepo.AddAsync(appointment);
        }

        var page1 = await _appointmentRepo.GetPagedAsync(1, 10);
        page1.Items.Should().HaveCount(10);
        page1.TotalCount.Should().Be(25);
        page1.PageNumber.Should().Be(1);
        page1.TotalPages.Should().Be(3);
        page1.HasPreviousPage.Should().BeFalse();
        page1.HasNextPage.Should().BeTrue();

        var page2 = await _appointmentRepo.GetPagedAsync(2, 10);
        page2.Items.Should().HaveCount(10);
        page2.TotalCount.Should().Be(25);

        var page3 = await _appointmentRepo.GetPagedAsync(3, 10);
        page3.Items.Should().HaveCount(5);
        page3.TotalCount.Should().Be(25);
        page3.HasPreviousPage.Should().BeTrue();
        page3.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public async Task GetPagedAsync_Appointments_EmptyCollection_ReturnsEmpty()
    {
        var result = await _appointmentRepo.GetPagedAsync(1, 10);
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedByPatientIdAsync_ReturnsCorrectPage()
    {
        var patient = CreatePatient("target@test.com");
        var doctor = CreateDoctor();

        for (int i = 0; i < 15; i++)
        {
            var appointmentTime = AppointmentTime.Create(
                DateTime.UtcNow.Date.AddDays(i + 30).AddHours(10));
            var appointment = Appointment.Create(
                patient, doctor, appointmentTime, $"Medical checkup #{i}",
                AppointmentCodeGenerator.Instance);
            await _appointmentRepo.AddAsync(appointment);
        }

        var page1 = await _appointmentRepo.GetPagedByPatientIdAsync(patient.Id, 1, 5);
        page1.Items.Should().HaveCount(5);
        page1.TotalCount.Should().Be(15);

        var page3 = await _appointmentRepo.GetPagedByPatientIdAsync(patient.Id, 3, 5);
        page3.Items.Should().HaveCount(5);
        page3.TotalCount.Should().Be(15);
    }

    [Fact]
    public async Task GetPagedByDoctorIdAsync_ReturnsCorrectPage()
    {
        var patient = CreatePatient("patient@test.com");
        var doctor = CreateDoctor();

        for (int i = 0; i < 7; i++)
        {
            var appointmentTime = AppointmentTime.Create(
                DateTime.UtcNow.Date.AddDays(i + 30).AddHours(10));
            var appointment = Appointment.Create(
                patient, doctor, appointmentTime, $"Medical checkup #{i}",
                AppointmentCodeGenerator.Instance);
            await _appointmentRepo.AddAsync(appointment);
        }

        var result = await _appointmentRepo.GetPagedByDoctorIdAsync(doctor.Id, 1, 5);
        result.Items.Should().HaveCount(5);
        result.TotalCount.Should().Be(7);

        var page2 = await _appointmentRepo.GetPagedByDoctorIdAsync(doctor.Id, 2, 5);
        page2.Items.Should().HaveCount(2);
        page2.TotalCount.Should().Be(7);
    }

    [Fact]
    public async Task GetPagedAsync_Patients_ReturnsCorrectPage()
    {
        for (int i = 0; i < 12; i++)
        {
            var patient = CreatePatient($"user{i}@test.com");
            await _patientRepo.AddAsync(patient);
        }

        var page1 = await _patientRepo.GetPagedAsync(1, 10);
        page1.Items.Should().HaveCount(10);
        page1.TotalCount.Should().Be(12);

        var page2 = await _patientRepo.GetPagedAsync(2, 10);
        page2.Items.Should().HaveCount(2);
        page2.TotalCount.Should().Be(12);

        var page3 = await _patientRepo.GetPagedAsync(3, 10);
        page3.Items.Should().BeEmpty();
        page3.TotalCount.Should().Be(12);
    }

    [Fact]
    public async Task GetPagedAsync_Payments_ReturnsCorrectPage()
    {
        var money = Money.Create(100, "USD");
        for (int i = 0; i < 20; i++)
        {
            var payment = Payment.Create(i + 1, money);
            await _paymentRepo.AddAsync(payment);
        }

        var page1 = await _paymentRepo.GetPagedAsync(1, 15);
        page1.Items.Should().HaveCount(15);
        page1.TotalCount.Should().Be(20);

        var page2 = await _paymentRepo.GetPagedAsync(2, 15);
        page2.Items.Should().HaveCount(5);
        page2.TotalCount.Should().Be(20);
    }

    private static Patient CreatePatient(string email)
    {
        return Patient.Create(
            "Test",
            "User",
            Email.Create(email),
            PhoneNumber.Create("+38349123456"),
            new DateTime(1990, 1, 1),
            Gender.Male,
            Address.Create("123 St", "City", "State", "10000", "Country"));
    }

    private static Doctor CreateDoctor()
    {
        return Doctor.Create(
            "Doctor",
            "Test",
            Email.Create("doctor@test.com"),
            PhoneNumber.Create("+38349987654"),
            "LIC-12345",
            Money.Create(50, "USD"),
            10,
            Specialty.GeneralPractice);
    }
}
