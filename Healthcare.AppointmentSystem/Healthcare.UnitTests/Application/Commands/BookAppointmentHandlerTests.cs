using FluentAssertions;
using Healthcare.Application.Commands.BookAppointment;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.UnitTests.Helpers;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Application.Commands;

/// <summary>
/// Unit tests for BookAppointmentHandler.
/// </summary>
public class BookAppointmentHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IDomainEventDispatcher> _eventDispatcherMock;
    private readonly BookAppointmentHandler _handler;

    public BookAppointmentHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _eventDispatcherMock = new Mock<IDomainEventDispatcher>();
        _handler = new BookAppointmentHandler(_unitOfWorkMock.Object, _eventDispatcherMock.Object);
    }

    #region Success Tests

    [Fact]
    public async Task HandleAsync_WithValidData_ShouldSucceed()
    {
        // Arrange
        var patient = TestDataBuilder.APatient().Build();
        var doctor = TestDataBuilder.ADoctor().Build();

        SetupPatientRepositoryMock(patient);
        SetupDoctorRepositoryMock(doctor);
        SetupAppointmentRepositoryMock(new List<Appointment>());

        var command = new BookAppointmentCommand
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            ScheduledTime = GetFutureWeekdayTime(),
            Reason = "Annual checkup and medical consultation"
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThan(0);

        _unitOfWorkMock.Verify(u => u.Appointments.AddAsync(
            It.IsAny<Appointment>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Once);

        _eventDispatcherMock.Verify(e => e.DispatchAsync(
            It.IsAny<IEnumerable<Healthcare.Domain.Common.IDomainEvent>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task HandleAsync_WithNonExistentPatient_ShouldReturnFailure()
    {
        // Arrange
        SetupPatientRepositoryMock(null);

        var command = new BookAppointmentCommand
        {
            PatientId = 999,
            DoctorId = 1,
            ScheduledTime = GetFutureWeekdayTime(),
            Reason = "Test reason that is long enough"
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Patient with ID 999 not found");

        _unitOfWorkMock.Verify(u => u.Appointments.AddAsync(
            It.IsAny<Appointment>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithNonExistentDoctor_ShouldReturnFailure()
    {
        // Arrange
        var patient = TestDataBuilder.APatient().Build();

        SetupPatientRepositoryMock(patient);
        SetupDoctorRepositoryMock(null);

        var command = new BookAppointmentCommand
        {
            PatientId = patient.Id,
            DoctorId = 999,
            ScheduledTime = GetFutureWeekdayTime(),
            Reason = "Test reason that is long enough"
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Doctor with ID 999 not found");

        _unitOfWorkMock.Verify(u => u.Appointments.AddAsync(
            It.IsAny<Appointment>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidAppointmentTime_ShouldReturnFailure()
    {
        // Arrange
        var patient = TestDataBuilder.APatient().Build();
        var doctor = TestDataBuilder.ADoctor().Build();

        SetupPatientRepositoryMock(patient);
        SetupDoctorRepositoryMock(doctor);

        var command = new BookAppointmentCommand
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            ScheduledTime = DateTime.Now.AddMinutes(30), // Less than 1 hour advance
            Reason = "Test reason that is long enough"
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid appointment time");
    }

    [Fact]
    public async Task HandleAsync_WhenDoctorNotAvailable_ShouldReturnFailure()
    {
        // Arrange
        var patient = TestDataBuilder.APatient().Build();
        var doctor = TestDataBuilder.ADoctor().Build();
        var scheduledTime = GetFutureWeekdayTime();

        // Create conflicting appointment at same time
        var existingAppointment = TestDataBuilder.AnAppointment()
            .WithDoctor(doctor)
            .WithScheduledTime(scheduledTime)
            .Build();

        SetupPatientRepositoryMock(patient);
        SetupDoctorRepositoryMock(doctor);
        SetupAppointmentRepositoryMock(new List<Appointment> { existingAppointment });

        var command = new BookAppointmentCommand
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            ScheduledTime = scheduledTime,
            Reason = "Test reason that is long enough"
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not available");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task HandleAsync_WithInactivePatient_ShouldReturnFailure()
    {
        // Arrange
        var patient = TestDataBuilder.APatient().Build();
        patient.Deactivate();

        var doctor = TestDataBuilder.ADoctor().Build();

        SetupPatientRepositoryMock(patient);
        SetupDoctorRepositoryMock(doctor);

        var command = new BookAppointmentCommand
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            ScheduledTime = GetFutureWeekdayTime(),
            Reason = "Test reason that is long enough"
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("inactive");
    }

    [Fact]
    public async Task HandleAsync_WithDoctorNotAcceptingPatients_ShouldReturnFailure()
    {
        // Arrange
        var patient = TestDataBuilder.APatient().Build();
        var doctor = TestDataBuilder.ADoctor().Build();
        doctor.StopAcceptingPatients();

        SetupPatientRepositoryMock(patient);
        SetupDoctorRepositoryMock(doctor);

        var command = new BookAppointmentCommand
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            ScheduledTime = GetFutureWeekdayTime(),
            Reason = "Test reason that is long enough"
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not accepting patients");
    }

    #endregion

    #region Helper Methods

    private void SetupPatientRepositoryMock(Patient? patient)
    {
        var patientRepoMock = new Mock<IPatientRepository>();
        patientRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        _unitOfWorkMock.Setup(u => u.Patients).Returns(patientRepoMock.Object);
    }

    private void SetupDoctorRepositoryMock(Doctor? doctor)
    {
        var doctorRepoMock = new Mock<IDoctorRepository>();
        doctorRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(doctor);

        _unitOfWorkMock.Setup(u => u.Doctors).Returns(doctorRepoMock.Object);
    }

    private void SetupAppointmentRepositoryMock(List<Appointment> existingAppointments)
    {
        var appointmentRepoMock = new Mock<IAppointmentRepository>();
        appointmentRepoMock
            .Setup(r => r.GetByDoctorAndDateAsync(
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAppointments);

        appointmentRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock.Setup(u => u.Appointments).Returns(appointmentRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private static DateTime GetFutureWeekdayTime()
    {
        var futureDate = DateTime.Today.AddDays(7);
        while (futureDate.DayOfWeek == DayOfWeek.Saturday ||
               futureDate.DayOfWeek == DayOfWeek.Sunday)
        {
            futureDate = futureDate.AddDays(1);
        }
        return futureDate.AddHours(10);
    }

    #endregion
}