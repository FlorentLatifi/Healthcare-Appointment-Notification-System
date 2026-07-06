using FluentAssertions;
using Healthcare.Adapters.Events;
using Healthcare.Adapters.Persistence.InMemory;
using Healthcare.Application.Common;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Payments;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Services;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.Services;
using Healthcare.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Services;

public class PaymentReconciliationServiceTests
{
    private readonly InMemoryUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly ILogger<PaymentReconciliationService> _logger;
    private readonly PaymentReconciliationService _service;

    public PaymentReconciliationServiceTests()
    {
        var appointmentRepo = new InMemoryAppointmentRepository();
        var patientRepo = new InMemoryPatientRepository();
        var doctorRepo = new InMemoryDoctorRepository();
        var userRepo = new InMemoryUserRepository();
        var paymentRepo = new InMemoryPaymentRepository();
        var auditLogRepo = new InMemoryAuditLogRepository();

        _unitOfWork = new InMemoryUnitOfWork(
            appointmentRepo, patientRepo, doctorRepo, userRepo, paymentRepo, auditLogRepo);

        var loggerMockDispatcher = new Mock<ILogger<DomainEventDispatcher>>();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        _eventDispatcher = new DomainEventDispatcher(serviceProvider, loggerMockDispatcher.Object);

        var loggerMock = new Mock<ILogger<PaymentReconciliationService>>();
        _logger = loggerMock.Object;

        _service = new PaymentReconciliationService(_unitOfWork, _eventDispatcher, _logger);
    }

    [Fact]
    public async Task ReconcilePaymentAsync_WithPendingAppointment_AndSuccess_ShouldMarkSucceededAndConfirm()
    {
        var (appointment, _, _) = await CreateSavedPendingAppointmentAsync();

        var result = await _service.ReconcilePaymentAsync(
            appointment.Id,
            "pi_test_123",
            succeeded: true,
            "txn_test_123",
            "card",
            failureReason: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThan(0);

        var payment = await _unitOfWork.Payments.GetByAppointmentIdAsync(appointment.Id);
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(PaymentStatus.Succeeded);

        var updatedAppointment = await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);
        updatedAppointment!.Status.Should().Be(AppointmentStatus.Confirmed);
    }

    [Fact]
    public async Task ReconcilePaymentAsync_WithCancelledAppointment_AndSuccess_ShouldStillSucceedPayment()
    {
        var (appointment, _, _) = await CreateSavedPendingAppointmentAsync();

        appointment.Cancel("Patient changed their mind");
        await _unitOfWork.Appointments.UpdateAsync(appointment);
        await _unitOfWork.SaveChangesAsync();

        var result = await _service.ReconcilePaymentAsync(
            appointment.Id,
            "pi_test_123",
            succeeded: true,
            "txn_test_123",
            "card",
            failureReason: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThan(0);

        var payment = await _unitOfWork.Payments.GetByAppointmentIdAsync(appointment.Id);
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(PaymentStatus.Succeeded);

        var updatedAppointment = await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);
        updatedAppointment!.Status.Should().Be(AppointmentStatus.Cancelled);
    }

    [Fact]
    public async Task ReconcilePaymentAsync_WithPendingAppointment_AndFailure_ShouldMarkFailed()
    {
        var (appointment, _, _) = await CreateSavedPendingAppointmentAsync();

        var result = await _service.ReconcilePaymentAsync(
            appointment.Id,
            "pi_test_123",
            succeeded: false,
            "txn_test_123",
            "card",
            "Insufficient funds");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Insufficient funds");

        var payment = await _unitOfWork.Payments.GetByAppointmentIdAsync(appointment.Id);
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(PaymentStatus.Failed);
        payment.FailureReason.Should().Be("Insufficient funds");
    }

    [Fact]
    public async Task ReconcilePaymentAsync_WithNonExistentAppointment_ShouldReturnFailure()
    {
        var result = await _service.ReconcilePaymentAsync(
            9999,
            "pi_test_123",
            succeeded: true,
            "txn_test_123",
            "card",
            failureReason: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ReconcilePaymentAsync_WithExistingSucceededPayment_ShouldReturnSuccessIdempotently()
    {
        var (appointment, _, _) = await CreateSavedPendingAppointmentAsync();

        var firstResult = await _service.ReconcilePaymentAsync(
            appointment.Id,
            "pi_test_123",
            succeeded: true,
            "txn_test_123",
            "card",
            failureReason: null);

        firstResult.IsSuccess.Should().BeTrue();

        var secondResult = await _service.ReconcilePaymentAsync(
            appointment.Id,
            "pi_test_456",
            succeeded: true,
            "txn_test_456",
            "card",
            failureReason: null);

        secondResult.IsSuccess.Should().BeTrue();
        secondResult.Value.Should().Be(firstResult.Value);

        var payments = await _unitOfWork.Payments.GetByAppointmentIdAsync(appointment.Id);
        payments!.Id.Should().Be(firstResult.Value);

        var updatedAppointment = await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);
        updatedAppointment!.Status.Should().Be(AppointmentStatus.Confirmed);
    }

    private static Patient CreateTestPatient()
    {
        return Patient.Create(
            "John",
            "Doe",
            Email.Create("patient@test.com"),
            PhoneNumber.Create("+38349123456"),
            new DateTime(1990, 1, 1),
            Gender.Male,
            Address.Create("Main St", "Pristina", "Kosovo", "10000", "Kosovo"));
    }

    private static Doctor CreateTestDoctor()
    {
        return Doctor.Create(
            "Jane",
            "Smith",
            Email.Create("doctor@test.com"),
            PhoneNumber.Create("+38349987654"),
            "LIC-123",
            Money.Create(50, "USD"),
            10,
            Specialty.GeneralPractice);
    }

    private static AppointmentTime CreateFutureAppointmentTime()
    {
        return AppointmentTime.Create(
            DateTime.Now.AddDays(5).Date.AddHours(10));
    }

    private async Task<(Appointment, Patient, Doctor)> CreateSavedPendingAppointmentAsync()
    {
        var patient = CreateTestPatient();
        var doctor = CreateTestDoctor();

        await _unitOfWork.Patients.AddAsync(patient);
        await _unitOfWork.Doctors.AddAsync(doctor);
        await _unitOfWork.SaveChangesAsync();

        var appointment = Appointment.Create(
            patient,
            doctor,
            CreateFutureAppointmentTime(),
            "General medical consultation",
            AppointmentCodeGenerator.Instance);

        appointment.ClearDomainEvents();

        await _unitOfWork.Appointments.AddAsync(appointment);
        await _unitOfWork.SaveChangesAsync();

        return (appointment, patient, doctor);
    }
}
