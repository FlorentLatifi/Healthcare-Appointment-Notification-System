using FluentAssertions;
using Healthcare.Adapters.Events;
using Healthcare.Adapters.Persistence.InMemory;
using Healthcare.Application.Commands.ConfirmAppointment;
using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.Services;
using Healthcare.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Application.Commands;

/// <summary>
/// Unit tests for ConfirmAppointmentHandler.
/// </summary>
/// <remarks>
/// Testing Strategy: Command Handler Pattern
///
/// What we test:
/// - Confirmation blocked when no successful payment exists and no override is given
/// - Confirmation succeeds once a Succeeded payment exists for the appointment
/// - Confirmation succeeds without payment when a Doctor/Admin override with a
///   valid reason (>= 10 chars) is supplied
/// - Override rejected when the reason is missing/too short
/// - Invalid confirmation from non-pending statuses (payment check is skipped
///   there — the domain's own state-transition error takes precedence)
/// - Appointment not found scenarios
/// - Domain events dispatching
/// </remarks>
public class ConfirmAppointmentHandlerTests
{
    private readonly InMemoryUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly ConfirmAppointmentHandler _handler;

    public ConfirmAppointmentHandlerTests()
    {
        // Repositories
        var appointmentRepo = new InMemoryAppointmentRepository();
        var patientRepo = new InMemoryPatientRepository();
        var doctorRepo = new InMemoryDoctorRepository();
        var userRepo = new InMemoryUserRepository();
        var paymentRepo = new InMemoryPaymentRepository();
        var auditLogRepo = new InMemoryAuditLogRepository();

        _unitOfWork = new InMemoryUnitOfWork(
            appointmentRepo,
            patientRepo,
            doctorRepo,
            userRepo,
            paymentRepo,
            auditLogRepo);

        // Event dispatcher
        var loggerMock = new Mock<ILogger<DomainEventDispatcher>>();
        var serviceProvider = CreateServiceProvider();
        _eventDispatcher = new DomainEventDispatcher(serviceProvider, loggerMock.Object);

        // Handler
        _handler = new ConfirmAppointmentHandler(_unitOfWork, _eventDispatcher);
    }

    #region Helper Methods

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

    private async Task<Appointment> CreateAndSavePendingAppointmentAsync()
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

        return appointment;
    }

    /// <summary>
    /// Creates and persists a Succeeded payment for the given appointment,
    /// satisfying the "must be paid before confirmation" business rule.
    /// </summary>
    private async Task AddSucceededPaymentAsync(int appointmentId, decimal amount = 50m, string currency = "USD")
    {
        var payment = Payment.Create(appointmentId, Money.Create(amount, currency), "Stripe");
        payment.MarkAsSucceeded(TransactionId.Create("pi_test_1234567890"), "card");
        payment.ClearDomainEvents();

        await _unitOfWork.Payments.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();
    }

    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        return services.BuildServiceProvider();
    }

    #endregion

    #region Success Tests

    [Fact]
    public async Task Handle_WithPendingAppointment_AndSucceededPayment_ShouldConfirmSuccessfully()
    {
        // Arrange
        var appointment = await CreateAndSavePendingAppointmentAsync();
        await AddSucceededPaymentAsync(appointment.Id);

        var command = new ConfirmAppointmentCommand
        {
            AppointmentId = appointment.Id
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var confirmedAppointment =
            await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);

        confirmedAppointment.Should().NotBeNull();
        confirmedAppointment!.Status.Should().Be(AppointmentStatus.Confirmed);
        confirmedAppointment.ConfirmedAt.Should().NotBeNull();
        confirmedAppointment.PaymentOverrideReason.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithPendingAppointment_AndNoPayment_AndValidOverride_ShouldConfirmSuccessfully()
    {
        // Arrange
        var appointment = await CreateAndSavePendingAppointmentAsync();

        var command = new ConfirmAppointmentCommand
        {
            AppointmentId = appointment.Id,
            OverridePaymentRequirement = true,
            OverrideReason = "Emergency walk-in, will settle payment after treatment"
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var confirmedAppointment =
            await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);

        confirmedAppointment!.Status.Should().Be(AppointmentStatus.Confirmed);
        confirmedAppointment.PaymentOverrideReason.Should().Be(command.OverrideReason);
    }

    #endregion

    #region Payment Rule Validation Tests

    [Fact]
    public async Task Handle_WithPendingAppointment_AndNoPayment_AndNoOverride_ShouldReturnFailure()
    {
        // Arrange
        var appointment = await CreateAndSavePendingAppointmentAsync();

        var command = new ConfirmAppointmentCommand
        {
            AppointmentId = appointment.Id
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("payment");

        var unchanged = await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);
        unchanged!.Status.Should().Be(AppointmentStatus.Pending);
    }

    [Fact]
    public async Task Handle_WithPendingAppointment_AndFailedPayment_AndNoOverride_ShouldReturnFailure()
    {
        // Arrange
        var appointment = await CreateAndSavePendingAppointmentAsync();

        var failedPayment = Payment.Create(appointment.Id, Money.Create(50, "USD"), "Stripe");
        failedPayment.MarkAsFailed("Card declined");
        failedPayment.ClearDomainEvents();
        await _unitOfWork.Payments.AddAsync(failedPayment);
        await _unitOfWork.SaveChangesAsync();

        var command = new ConfirmAppointmentCommand
        {
            AppointmentId = appointment.Id
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("payment");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too short")]
    public async Task Handle_WithOverrideRequested_AndInvalidReason_ShouldReturnFailure(string? reason)
    {
        // Arrange
        var appointment = await CreateAndSavePendingAppointmentAsync();

        var command = new ConfirmAppointmentCommand
        {
            AppointmentId = appointment.Id,
            OverridePaymentRequirement = true,
            OverrideReason = reason
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("at least 10 characters");

        var unchanged = await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);
        unchanged!.Status.Should().Be(AppointmentStatus.Pending);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task Handle_WithNonExistentAppointment_ShouldReturnFailure()
    {
        // Arrange
        var command = new ConfirmAppointmentCommand
        {
            AppointmentId = 9999
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    #endregion

    #region Invalid State Tests

    [Fact]
    public async Task Handle_WithAlreadyConfirmedAppointment_ShouldReturnFailure()
    {
        // Arrange
        var appointment = await CreateAndSavePendingAppointmentAsync();
        await AddSucceededPaymentAsync(appointment.Id);
        appointment.Confirm();

        await _unitOfWork.Appointments.UpdateAsync(appointment);
        await _unitOfWork.SaveChangesAsync();

        var command = new ConfirmAppointmentCommand
        {
            AppointmentId = appointment.Id
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Confirmed");
    }

    [Fact]
    public async Task Handle_WithCancelledAppointment_ShouldReturnFailure()
    {
        // Arrange
        var appointment = await CreateAndSavePendingAppointmentAsync();
        appointment.Cancel("Patient requested cancellation");

        await _unitOfWork.Appointments.UpdateAsync(appointment);
        await _unitOfWork.SaveChangesAsync();

        var command = new ConfirmAppointmentCommand
        {
            AppointmentId = appointment.Id
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Cancelled");
    }

    #endregion

    #region Domain Events Tests

    [Fact]
    public async Task Handle_ShouldClearDomainEventsAfterDispatch()
    {
        // Arrange
        var appointment = await CreateAndSavePendingAppointmentAsync();
        await AddSucceededPaymentAsync(appointment.Id);

        var command = new ConfirmAppointmentCommand
        {
            AppointmentId = appointment.Id
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        var updated =
            await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);

        updated!.DomainEvents.Should().BeEmpty();
    }

    #endregion
}