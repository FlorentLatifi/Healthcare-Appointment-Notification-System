using FluentAssertions;
using Healthcare.Adapters.Events;
using Healthcare.Adapters.Persistence.InMemory;
using Healthcare.Application.Commands.ConfirmAppointment;
using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
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
/// - Successful confirmation from Pending status
/// - Invalid confirmation from non-pending statuses
/// - Appointment not found scenarios
/// - Domain events dispatching
/// - State transition correctness
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

        _unitOfWork = new InMemoryUnitOfWork(
            appointmentRepo,
            patientRepo,
            doctorRepo,
            userRepo);

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
            "General medical consultation");

        appointment.ClearDomainEvents();

        await _unitOfWork.Appointments.AddAsync(appointment);
        await _unitOfWork.SaveChangesAsync();

        return appointment;
    }

    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        return services.BuildServiceProvider();
    }

    #endregion

    #region Success Tests

    [Fact]
    public async Task Handle_WithPendingAppointment_ShouldConfirmSuccessfully()
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
        result.IsSuccess.Should().BeTrue();

        var confirmedAppointment =
            await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);

        confirmedAppointment.Should().NotBeNull();
        confirmedAppointment!.Status.Should().Be(AppointmentStatus.Confirmed);
        confirmedAppointment.ConfirmedAt.Should().NotBeNull();
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

