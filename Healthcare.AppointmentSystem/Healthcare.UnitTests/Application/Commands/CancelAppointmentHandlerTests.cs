using FluentAssertions;
using Healthcare.Adapters.Events;
using Healthcare.Adapters.Persistence.InMemory;
using Healthcare.Application.Commands.CancelAppointment;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Application.Commands;

/// <summary>
/// Unit tests for CancelAppointmentHandler.
/// </summary>
/// <remarks>
/// Testing Strategy: Command Handler Pattern
/// 
/// What we test:
/// - Successful cancellation from Pending status
/// - Successful cancellation from Confirmed status
/// - Invalid cancellation from terminal statuses
/// - Appointment not found scenarios
/// - Domain events dispatching
/// - Validation of cancellation reasons
/// </remarks>
public class CancelAppointmentHandlerTests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly CancelAppointmentHandler _handler;

    public CancelAppointmentHandlerTests()
    {
        // Setup repositories
        var appointmentRepo = new InMemoryAppointmentRepository();
        var patientRepo = new InMemoryPatientRepository();
        var doctorRepo = new InMemoryDoctorRepository();
        var userRepo = new InMemoryUserRepository();

        _unitOfWork = new InMemoryUnitOfWork(
            appointmentRepo,
            patientRepo,
            doctorRepo,
            userRepo);

        // Setup event dispatcher (with mock logger)
        var mockLogger = new Mock<ILogger<DomainEventDispatcher>>();
        var serviceProvider = CreateServiceProvider();
        _eventDispatcher = new DomainEventDispatcher(serviceProvider, mockLogger.Object);

        // Create handler
        _handler = new CancelAppointmentHandler(_unitOfWork, _eventDispatcher);
    }

    #region Helper Methods

    private static Patient CreateTestPatient()
    {
        var email = Email.Create("patient@test.com");
        var phone = PhoneNumber.Create("+38349123456");
        var address = Address.Create("123 Main St", "Pristina", "Kosovo", "10000", "Kosovo");

        return Patient.Create(
            "John",
            "Doe",
            email,
            phone,
            new DateTime(1990, 1, 1),
            Gender.Male,
            address);
    }

    private static Doctor CreateTestDoctor()
    {
        var email = Email.Create("doctor@test.com");
        var phone = PhoneNumber.Create("+38349987654");
        var fee = Money.Create(50, "USD");

        return Doctor.Create(
            "Jane",
            "Smith",
            email,
            phone,
            "LIC-12345",
            fee,
            10,
            Specialty.GeneralPractice);
    }

    private static AppointmentTime CreateFutureAppointmentTime()
    {
        var futureDate = DateTime.Now.AddDays(7).Date;

       
        while (futureDate.DayOfWeek == DayOfWeek.Saturday ||
               futureDate.DayOfWeek == DayOfWeek.Sunday)
        {
            futureDate = futureDate.AddDays(1);
        }

        return AppointmentTime.Create(futureDate.AddHours(10));
    }

    private async Task<Appointment> CreateAndSaveAppointmentAsync(AppointmentStatus status = AppointmentStatus.Pending)
    {
        var patient = CreateTestPatient();
        var doctor = CreateTestDoctor();

        await _unitOfWork.Patients.AddAsync(patient);
        await _unitOfWork.Doctors.AddAsync(doctor);
        await _unitOfWork.SaveChangesAsync();

        var scheduledTime = CreateFutureAppointmentTime();
        var appointment = Appointment.Create(
            patient,
            doctor,
            scheduledTime,
            "Annual checkup and consultation");

        if (status == AppointmentStatus.Confirmed)
        {
            appointment.Confirm();
        }

        appointment.ClearDomainEvents(); // Clear creation/confirmation events

        await _unitOfWork.Appointments.AddAsync(appointment);
        await _unitOfWork.SaveChangesAsync();

        return appointment;
    }

    private static IServiceProvider CreateServiceProvider()
    {
        // Minimal service provider for event dispatcher
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        return services.BuildServiceProvider();
    }

    #endregion

    #region Successful Cancellation Tests

    [Fact]
    public async Task Handle_WithPendingAppointment_ShouldCancelSuccessfully()
    {
        // Arrange
        var appointment = await CreateAndSaveAppointmentAsync(AppointmentStatus.Pending);

        var command = new CancelAppointmentCommand
        {
            AppointmentId = appointment.Id,
            CancellationReason = "Patient requested cancellation due to scheduling conflict"
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        var cancelledAppointment = await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);
        cancelledAppointment.Should().NotBeNull();
        cancelledAppointment!.Status.Should().Be(AppointmentStatus.Cancelled);
        cancelledAppointment.CancellationReason.Should().Be(command.CancellationReason);
        cancelledAppointment.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WithConfirmedAppointment_ShouldCancelSuccessfully()
    {
        // Arrange
        var appointment = await CreateAndSaveAppointmentAsync(AppointmentStatus.Confirmed);

        var command = new CancelAppointmentCommand
        {
            AppointmentId = appointment.Id,
            CancellationReason = "Doctor unavailable due to emergency situation"
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        var cancelledAppointment = await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);
        cancelledAppointment.Should().NotBeNull();
        cancelledAppointment!.Status.Should().Be(AppointmentStatus.Cancelled);
        cancelledAppointment.CancellationReason.Should().Be(command.CancellationReason);
    }

    [Fact]
    public async Task Handle_ShouldPersistCancellationReason()
    {
        // Arrange
        var appointment = await CreateAndSaveAppointmentAsync();

        const string cancellationReason = "Patient requested cancellation due to travel plans";
        var command = new CancelAppointmentCommand
        {
            AppointmentId = appointment.Id,
            CancellationReason = cancellationReason
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        var updatedAppointment = await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);
        updatedAppointment!.CancellationReason.Should().Be(cancellationReason);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task Handle_WithNonExistentAppointment_ShouldReturnFailure()
    {
        // Arrange
        var command = new CancelAppointmentCommand
        {
            AppointmentId = 9999, // Non-existent ID
            CancellationReason = "Patient requested cancellation"
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        result.Error.Should().Contain("9999");
    }

    [Theory]
    [InlineData("Short")] // Too short
    [InlineData("123456789")] // 9 characters
    public async Task Handle_WithTooShortCancellationReason_ShouldReturnFailure(string shortReason)
    {
        // Arrange
        var appointment = await CreateAndSaveAppointmentAsync();

        var command = new CancelAppointmentCommand
        {
            AppointmentId = appointment.Id,
            CancellationReason = shortReason
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("at least 10 characters");
    }

    #endregion

    #region Invalid State Tests

    [Fact]
    public async Task Handle_WithCompletedAppointment_ShouldReturnFailure()
    {
        // Arrange
        var appointment = await CreateAndSaveAppointmentAsync(AppointmentStatus.Confirmed);

        // Complete the appointment
        appointment.Complete("Examination completed successfully with no issues found.");
        await _unitOfWork.Appointments.UpdateAsync(appointment);
        await _unitOfWork.SaveChangesAsync();

        var command = new CancelAppointmentCommand
        {
            AppointmentId = appointment.Id,
            CancellationReason = "Trying to cancel completed appointment"
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cancel");
        result.Error.Should().Contain("Completed");
    }

    [Fact]
    public async Task Handle_WithAlreadyCancelledAppointment_ShouldReturnFailure()
    {
        // Arrange
        var appointment = await CreateAndSaveAppointmentAsync();

        // Cancel once
        appointment.Cancel("First cancellation reason that is long enough");
        await _unitOfWork.Appointments.UpdateAsync(appointment);
        await _unitOfWork.SaveChangesAsync();

        // Try to cancel again
        var command = new CancelAppointmentCommand
        {
            AppointmentId = appointment.Id,
            CancellationReason = "Trying to cancel already cancelled appointment"
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Cancelled");
    }

    #endregion

    #region Domain Events Tests

    [Fact]
    public async Task Handle_ShouldClearDomainEventsAfterDispatching()
    {
        // Arrange
        var appointment = await CreateAndSaveAppointmentAsync();

        var command = new CancelAppointmentCommand
        {
            AppointmentId = appointment.Id,
            CancellationReason = "Patient requested cancellation due to emergency"
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        var updatedAppointment = await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);
        updatedAppointment!.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Handle_WithExactly10CharacterReason_ShouldSucceed()
    {
        // Arrange
        var appointment = await CreateAndSaveAppointmentAsync();

        var command = new CancelAppointmentCommand
        {
            AppointmentId = appointment.Id,
            CancellationReason = "1234567890" // Exactly 10 characters
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithVeryLongReason_ShouldSucceed()
    {
        // Arrange
        var appointment = await CreateAndSaveAppointmentAsync();

        var longReason = new string('a', 500);
        var command = new CancelAppointmentCommand
        {
            AppointmentId = appointment.Id,
            CancellationReason = longReason
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        var updatedAppointment = await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);
        updatedAppointment!.CancellationReason.Should().HaveLength(500);
    }

    #endregion
}