using FluentAssertions;
using Healthcare.Domain.Common;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.Events;
using Healthcare.Domain.ValueObjects;
using Xunit;

namespace Healthcare.UnitTests.Domain.Entities;

/// <summary>
/// Unit tests for Appointment entity (Aggregate Root) - FIXED VERSION
/// </summary>
public class AppointmentTests
{
    #region Test Data Helpers

    private const string ValidReason = "Annual checkup and consultation";

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

    /// <summary>
    /// Creates a valid future appointment time (ALWAYS on a weekday).
    /// </summary>
    private static AppointmentTime CreateFutureAppointmentTime()
    {
        var futureDate = DateTime.Now.AddDays(7).Date;

        // ✅ CRITICAL FIX: Skip weekends
        while (futureDate.DayOfWeek == DayOfWeek.Saturday ||
               futureDate.DayOfWeek == DayOfWeek.Sunday)
        {
            futureDate = futureDate.AddDays(1);
        }

        return AppointmentTime.Create(futureDate.AddHours(10));
    }

    #endregion

    #region Creation Tests

    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        // Arrange
        var patient = CreateTestPatient();
        var doctor = CreateTestDoctor();
        var scheduledTime = CreateFutureAppointmentTime();

        // Act
        var appointment = Appointment.Create(patient, doctor, scheduledTime, ValidReason);

        // Assert
        appointment.Should().NotBeNull();
        appointment.PatientId.Should().Be(patient.Id);
        appointment.DoctorId.Should().Be(doctor.Id);
        appointment.ScheduledTime.Should().Be(scheduledTime);
        appointment.Reason.Should().Be(ValidReason);
        appointment.Status.Should().Be(AppointmentStatus.Pending);
        appointment.ConsultationFee.Should().Be(doctor.ConsultationFee);
    }

    [Fact]
    public void Create_ShouldRaiseAppointmentCreatedEvent()
    {
        // Arrange
        var patient = CreateTestPatient();
        var doctor = CreateTestDoctor();
        var scheduledTime = CreateFutureAppointmentTime();

        // Act
        var appointment = Appointment.Create(patient, doctor, scheduledTime, ValidReason);

        // Assert
        appointment.DomainEvents.Should().ContainSingle();
        appointment.DomainEvents.First().Should().BeOfType<AppointmentCreatedEvent>();

        var createdEvent = appointment.DomainEvents.First() as AppointmentCreatedEvent;
        createdEvent.Should().NotBeNull();
        createdEvent!.AppointmentId.Should().Be(appointment.Id);
        createdEvent.PatientId.Should().Be(patient.Id);
        createdEvent.DoctorId.Should().Be(doctor.Id);
    }

    [Fact]
    public void Create_WithInactivePatient_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var patient = CreateTestPatient();
        patient.Deactivate();

        var doctor = CreateTestDoctor();
        var scheduledTime = CreateFutureAppointmentTime();

        // Act
        Action act = () => Appointment.Create(patient, doctor, scheduledTime, ValidReason);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*patient account is inactive*");
    }

    [Fact]
    public void Create_WithInactiveDoctor_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var patient = CreateTestPatient();
        var doctor = CreateTestDoctor();
        doctor.Deactivate();

        var scheduledTime = CreateFutureAppointmentTime();

        // Act
        Action act = () => Appointment.Create(patient, doctor, scheduledTime, ValidReason);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*doctor is not active*");
    }

    [Fact]
    public void Create_WithDoctorNotAcceptingPatients_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var patient = CreateTestPatient();
        var doctor = CreateTestDoctor();
        doctor.StopAcceptingPatients();

        var scheduledTime = CreateFutureAppointmentTime();

        // Act
        Action act = () => Appointment.Create(patient, doctor, scheduledTime, ValidReason);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not accepting patients*");
    }

    [Theory]
    [InlineData("Short")]
    [InlineData("123456789")]
    public void Create_WithTooShortReason_ShouldThrowArgumentException(string shortReason)
    {
        // Arrange
        var patient = CreateTestPatient();
        var doctor = CreateTestDoctor();
        var scheduledTime = CreateFutureAppointmentTime();

        // Act
        Action act = () => Appointment.Create(patient, doctor, scheduledTime, shortReason);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least 10 characters*");
    }

    #endregion

    #region State Transition Tests - Confirm

    [Fact]
    public void Confirm_FromPendingStatus_ShouldSucceed()
    {
        // Arrange
        var appointment = Appointment.Create(
            CreateTestPatient(),
            CreateTestDoctor(),
            CreateFutureAppointmentTime(),
            ValidReason);

        appointment.ClearDomainEvents();

        // Act
        appointment.Confirm();

        // Assert
        appointment.Status.Should().Be(AppointmentStatus.Confirmed);
        appointment.ConfirmedAt.Should().NotBeNull();
        appointment.ConfirmedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Confirm_ShouldRaiseAppointmentConfirmedEvent()
    {
        // Arrange
        var appointment = Appointment.Create(
            CreateTestPatient(),
            CreateTestDoctor(),
            CreateFutureAppointmentTime(),
            ValidReason);

        appointment.ClearDomainEvents();

        // Act
        appointment.Confirm();

        // Assert
        appointment.DomainEvents.Should().ContainSingle();
        appointment.DomainEvents.First().Should().BeOfType<AppointmentConfirmedEvent>();
    }

    [Theory]
    [InlineData(AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public void Confirm_FromNonPendingStatus_ShouldThrowInvalidAppointmentStateException(
        AppointmentStatus invalidStatus)
    {
        // Arrange
        var appointment = Appointment.Create(
            CreateTestPatient(),
            CreateTestDoctor(),
            CreateFutureAppointmentTime(),
            ValidReason);

        var statusProperty = typeof(Appointment).GetProperty("Status");
        statusProperty.Should().NotBeNull();
        statusProperty!.SetValue(appointment, invalidStatus);

        // Act
        Action act = () => appointment.Confirm();

        // Assert
        act.Should().Throw<InvalidAppointmentStateException>()
            .WithMessage($"*{invalidStatus}*");
    }

    #endregion

    #region State Transition Tests - Cancel

    [Theory]
    [InlineData(AppointmentStatus.Pending)]
    [InlineData(AppointmentStatus.Confirmed)]
    public void Cancel_FromPendingOrConfirmed_ShouldSucceed(AppointmentStatus startStatus)
    {
        // Arrange
        var appointment = Appointment.Create(
            CreateTestPatient(),
            CreateTestDoctor(),
            CreateFutureAppointmentTime(),
            ValidReason);

        if (startStatus == AppointmentStatus.Confirmed)
        {
            appointment.Confirm();
        }

        appointment.ClearDomainEvents();

        const string cancellationReason = "Patient requested reschedule due to conflict";

        // Act
        appointment.Cancel(cancellationReason);

        // Assert
        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
        appointment.CancellationReason.Should().Be(cancellationReason);
        appointment.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_ShouldRaiseAppointmentCancelledEvent()
    {
        // Arrange
        var appointment = Appointment.Create(
            CreateTestPatient(),
            CreateTestDoctor(),
            CreateFutureAppointmentTime(),
            ValidReason);

        appointment.ClearDomainEvents();

        // Act
        appointment.Cancel("Emergency situation came up suddenly");

        // Assert
        appointment.DomainEvents.Should().ContainSingle();
        appointment.DomainEvents.First().Should().BeOfType<AppointmentCancelledEvent>();
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public void Cancel_FromTerminalStatus_ShouldThrowInvalidAppointmentStateException(
        AppointmentStatus terminalStatus)
    {
        // Arrange
        var appointment = Appointment.Create(
            CreateTestPatient(),
            CreateTestDoctor(),
            CreateFutureAppointmentTime(),
            ValidReason);

        typeof(Appointment)
            .GetProperty("Status")!
            .SetValue(appointment, terminalStatus);

        // Act
        Action act = () => appointment.Cancel("Trying to cancel completed appointment");

        // Assert
        act.Should().Throw<InvalidAppointmentStateException>();
    }

    [Theory]
    [InlineData("Short")]
    [InlineData("123456789")]
    public void Cancel_WithTooShortReason_ShouldThrowArgumentException(string shortReason)
    {
        // Arrange
        var appointment = Appointment.Create(
            CreateTestPatient(),
            CreateTestDoctor(),
            CreateFutureAppointmentTime(),
            ValidReason);

        // Act
        Action act = () => appointment.Cancel(shortReason);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least 10 characters*");
    }

    #endregion

    #region State Transition Tests - Complete

    [Fact]
    public void Complete_FromConfirmedStatus_ShouldSucceed()
    {
        // Arrange
        var appointment = Appointment.Create(
            CreateTestPatient(),
            CreateTestDoctor(),
            CreateFutureAppointmentTime(),
            ValidReason);

        appointment.Confirm();
        appointment.ClearDomainEvents();

        const string doctorNotes = "Patient is healthy. No issues found during examination.";

        // Act
        appointment.Complete(doctorNotes);

        // Assert
        appointment.Status.Should().Be(AppointmentStatus.Completed);
        appointment.DoctorNotes.Should().Be(doctorNotes);
        appointment.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Complete_ShouldRaiseAppointmentCompletedEvent()
    {
        // Arrange
        var appointment = Appointment.Create(
            CreateTestPatient(),
            CreateTestDoctor(),
            CreateFutureAppointmentTime(),
            ValidReason);

        appointment.Confirm();
        appointment.ClearDomainEvents();

        // Act
        appointment.Complete("Examination completed successfully with no complications.");

        // Assert
        appointment.DomainEvents.Should().ContainSingle();
        appointment.DomainEvents.First().Should().BeOfType<AppointmentCompletedEvent>();
    }

    [Theory]
    [InlineData(AppointmentStatus.Pending)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public void Complete_FromNonConfirmedStatus_ShouldThrowInvalidAppointmentStateException(
        AppointmentStatus invalidStatus)
    {
        // Arrange
        var appointment = Appointment.Create(
            CreateTestPatient(),
            CreateTestDoctor(),
            CreateFutureAppointmentTime(),
            ValidReason);

        typeof(Appointment)
            .GetProperty("Status")!
            .SetValue(appointment, invalidStatus);

        // Act
        Action act = () => appointment.Complete("Some notes that are definitely long enough here");

        // Assert
        act.Should().Throw<InvalidAppointmentStateException>();
    }

    [Theory]
    [InlineData("Short notes")]
    [InlineData("1234567890123456789")]
    public void Complete_WithTooShortNotes_ShouldThrowArgumentException(string shortNotes)
    {
        // Arrange
        var appointment = Appointment.Create(
            CreateTestPatient(),
            CreateTestDoctor(),
            CreateFutureAppointmentTime(),
            ValidReason);

        appointment.Confirm();

        // Act
        Action act = () => appointment.Complete(shortNotes);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least 20 characters*");
    }

    #endregion

    #region Terminal State Tests

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public void IsTerminal_WithTerminalStatus_ShouldReturnTrue(AppointmentStatus status)
    {
        // Arrange
        var appointment = Appointment.Create(
            CreateTestPatient(),
            CreateTestDoctor(),
            CreateFutureAppointmentTime(),
            ValidReason);

        typeof(Appointment)
            .GetProperty("Status")!
            .SetValue(appointment, status);

        // Act
        var result = appointment.IsTerminal();

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(AppointmentStatus.Pending)]
    [InlineData(AppointmentStatus.Confirmed)]
    public void IsTerminal_WithNonTerminalStatus_ShouldReturnFalse(AppointmentStatus status)
    {
        // Arrange
        var appointment = Appointment.Create(
            CreateTestPatient(),
            CreateTestDoctor(),
            CreateFutureAppointmentTime(),
            ValidReason);

        typeof(Appointment)
            .GetProperty("Status")!
            .SetValue(appointment, status);

        // Act
        var result = appointment.IsTerminal();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Domain Events Tests

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAllEvents()
    {
        // Arrange
        var appointment = Appointment.Create(
            CreateTestPatient(),
            CreateTestDoctor(),
            CreateFutureAppointmentTime(),
            ValidReason);

        appointment.DomainEvents.Should().HaveCount(1);

        // Act
        appointment.ClearDomainEvents();

        // Assert
        appointment.DomainEvents.Should().BeEmpty();
    }

    #endregion
}