using FluentAssertions;
using Healthcare.Adapters.Events;
using Healthcare.Adapters.Persistence.InMemory;
using Healthcare.Application.Commands.ProcessPayment;
using Healthcare.Application.Common;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Payments;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.Services;
using Healthcare.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Application.Commands;

public class ProcessPaymentHandlerTests
{
    private readonly InMemoryUnitOfWork _unitOfWork;
    private readonly Mock<IPaymentGateway> _paymentGatewayMock;
    private readonly Mock<ILogger<ProcessPaymentHandler>> _loggerMock;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly ProcessPaymentHandler _handler;

    public ProcessPaymentHandlerTests()
    {
        var appointmentRepo = new InMemoryAppointmentRepository();
        var patientRepo = new InMemoryPatientRepository();
        var doctorRepo = new InMemoryDoctorRepository();
        var userRepo = new InMemoryUserRepository();
        var paymentRepo = new InMemoryPaymentRepository();
        var auditLogRepo = new InMemoryAuditLogRepository();

        _unitOfWork = new InMemoryUnitOfWork(
            appointmentRepo, patientRepo, doctorRepo, userRepo, paymentRepo, auditLogRepo);

        _paymentGatewayMock = new Mock<IPaymentGateway>();
        _loggerMock = new Mock<ILogger<ProcessPaymentHandler>>();

        var loggerMockDispatcher = new Mock<ILogger<DomainEventDispatcher>>();
        var serviceProvider = CreateServiceProvider();
        _eventDispatcher = new DomainEventDispatcher(serviceProvider, loggerMockDispatcher.Object);

        _handler = new ProcessPaymentHandler(
            _unitOfWork, _paymentGatewayMock.Object, _eventDispatcher, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithPendingAppointment_AndGatewaySucceeds_ShouldPersistAndReturnSuccess()
    {
        var (appointment, _, _) = await CreateSavedPendingAppointmentAsync();

        _paymentGatewayMock
            .Setup(g => g.ConfirmPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentConfirmationResult>.Success(new PaymentConfirmationResult
            {
                Succeeded = true,
                TransactionId = "txn_test_1234567890",
                PaymentMethod = "card"
            }));

        var command = new ProcessPaymentCommand
        {
            AppointmentId = appointment.Id,
            PaymentIntentId = "pi_test_1234567890"
        };

        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThan(0);

        var payment = await _unitOfWork.Payments.GetByAppointmentIdAsync(appointment.Id);
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(PaymentStatus.Succeeded);

        var updatedAppointment = await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);
        updatedAppointment!.Status.Should().Be(AppointmentStatus.Confirmed);
    }

    [Fact]
    public async Task Handle_WithCancelledAppointment_AndGatewaySucceeds_ShouldStillPersistPaymentAndReturnSuccess()
    {
        var (appointment, _, _) = await CreateSavedPendingAppointmentAsync();

        appointment.Cancel("Patient changed their mind");
        await _unitOfWork.Appointments.UpdateAsync(appointment);
        await _unitOfWork.SaveChangesAsync();

        _paymentGatewayMock
            .Setup(g => g.ConfirmPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentConfirmationResult>.Success(new PaymentConfirmationResult
            {
                Succeeded = true,
                TransactionId = "txn_test_1234567890",
                PaymentMethod = "card"
            }));

        var command = new ProcessPaymentCommand
        {
            AppointmentId = appointment.Id,
            PaymentIntentId = "pi_test_1234567890"
        };

        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThan(0);

        var payment = await _unitOfWork.Payments.GetByAppointmentIdAsync(appointment.Id);
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(PaymentStatus.Succeeded);

        var updatedAppointment = await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);
        updatedAppointment!.Status.Should().Be(AppointmentStatus.Cancelled);

        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithPendingAppointment_AndGatewayFailure_ShouldReturnFailureAndPersistFailedPayment()
    {
        var (appointment, _, _) = await CreateSavedPendingAppointmentAsync();

        _paymentGatewayMock
            .Setup(g => g.ConfirmPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentConfirmationResult>.Success(new PaymentConfirmationResult
            {
                Succeeded = false,
                TransactionId = "txn_test_1234567890",
                PaymentMethod = "card",
                FailureReason = "Insufficient funds"
            }));

        var command = new ProcessPaymentCommand
        {
            AppointmentId = appointment.Id,
            PaymentIntentId = "pi_test_1234567890"
        };

        var result = await _handler.HandleAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Insufficient funds");

        var payment = await _unitOfWork.Payments.GetByAppointmentIdAsync(appointment.Id);
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(PaymentStatus.Failed);
        payment.FailureReason.Should().Be("Insufficient funds");
    }

    [Fact]
    public async Task Handle_WithNonExistentAppointment_ShouldReturnFailure()
    {
        _paymentGatewayMock
            .Setup(g => g.ConfirmPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentConfirmationResult>.Success(new PaymentConfirmationResult
            {
                Succeeded = true,
                TransactionId = "txn_test_1234567890",
                PaymentMethod = "card"
            }));

        var command = new ProcessPaymentCommand
        {
            AppointmentId = 9999,
            PaymentIntentId = "pi_test_1234567890"
        };

        var result = await _handler.HandleAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WithGatewayConfirmationFailure_ShouldReturnFailure()
    {
        var (appointment, _, _) = await CreateSavedPendingAppointmentAsync();

        _paymentGatewayMock
            .Setup(g => g.ConfirmPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentConfirmationResult>.Failure("Gateway timeout"));

        var command = new ProcessPaymentCommand
        {
            AppointmentId = appointment.Id,
            PaymentIntentId = "pi_test_1234567890"
        };

        var result = await _handler.HandleAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Gateway timeout");
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

    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        return services.BuildServiceProvider();
    }
}
