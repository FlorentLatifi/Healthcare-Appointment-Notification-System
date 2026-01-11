using FluentAssertions;
using Healthcare.Adapters.Persistence.InMemory;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Queries.GetAppointment;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Xunit;

namespace Healthcare.UnitTests.Application.Queries;

/// <summary>
/// Unit tests for GetAppointmentHandler.
/// </summary>
/// <remarks>
/// Testing Strategy: Query Handler Pattern
/// 
/// What we test:
/// - Successful retrieval of existing appointment
/// - Not found scenario
/// - Correct DTO mapping
/// - Data integrity (all fields mapped correctly)
/// - Navigation properties (Patient, Doctor)
/// </remarks>
public class GetAppointmentHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly IDoctorRepository _doctorRepository;
    private readonly GetAppointmentHandler _handler;

    public GetAppointmentHandlerTests()
    {
        // Setup repositories
        _appointmentRepository = new InMemoryAppointmentRepository();
        _patientRepository = new InMemoryPatientRepository();
        _doctorRepository = new InMemoryDoctorRepository();

        // Create handler
        _handler = new GetAppointmentHandler(_appointmentRepository);
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

        await _patientRepository.AddAsync(patient);
        await _doctorRepository.AddAsync(doctor);

        var scheduledTime = CreateFutureAppointmentTime();
        var appointment = Appointment.Create(
            patient,
            doctor,
            scheduledTime,
            "Annual checkup and blood pressure monitoring");

        if (status == AppointmentStatus.Confirmed)
        {
            appointment.Confirm();
        }

        await _appointmentRepository.AddAsync(appointment);

        return appointment;
    }

    #endregion

    #region Successful Retrieval Tests

    [Fact]
    public async Task Handle_WithExistingAppointment_ShouldReturnSuccessWithDto()
    {
        // Arrange
        var appointment = await CreateAndSaveAppointmentAsync();

        var query = new GetAppointmentQuery(appointment.Id);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ShouldMapAppointmentIdCorrectly()
    {
        // Arrange
        var appointment = await CreateAndSaveAppointmentAsync();
        var query = new GetAppointmentQuery(appointment.Id);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Value.Id.Should().Be(appointment.Id);
    }

    [Fact]
    public async Task Handle_ShouldMapPatientInformationCorrectly()
    {
        // Arrange
        var appointment = await CreateAndSaveAppointmentAsync();
        var query = new GetAppointmentQuery(appointment.Id);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        var dto = result.Value;
        dto.Patient.Should().NotBeNull();
        dto.Patient.Id.Should().Be(appointment.Patient.Id);
        dto.Patient.FirstName.Should().Be(appointment.Patient.FirstName);
        dto.Patient.LastName.Should().Be(appointment.Patient.LastName);
        dto.Patient.FullName.Should().Be(appointment.Patient.FullName);
        dto.Patient.Email.Should().Be(appointment.Patient.Email.Value);
        dto.Patient.PhoneNumber.Should().Be(appointment.Patient.PhoneNumber.Value);
        dto.Patient.Age.Should().Be(appointment.Patient.Age);
        dto.Patient.Gender.Should().Be(appointment.Patient.Gender.ToString());
        dto.Patient.IsActive.Should().Be(appointment.Patient.IsActive);
    }

    [Fact]
    public async Task Handle_ShouldMapDoctorInformationCorrectly()
    {
        // Arrange
        var appointment = await CreateAndSaveAppointmentAsync();
        var query = new GetAppointmentQuery(appointment.Id);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        var dto = result.Value;
        dto.Doctor.Should().NotBeNull();
        dto.Doctor.Id.Should().Be(appointment.Doctor.Id);
        dto.Doctor.FirstName.Should().Be(appointment.Doctor.FirstName);
        dto.Doctor.LastName.Should().Be(appointment.Doctor.LastName);
        dto.Doctor.FullName.Should().Be(appointment.Doctor.FullName);
        dto.Doctor.Email.Should().Be(appointment.Doctor.Email.Value);
        dto.Doctor.PhoneNumber.Should().Be(appointment.Doctor.PhoneNumber.Value);
        dto.Doctor.LicenseNumber.Should().Be(appointment.Doctor.LicenseNumber);
        dto.Doctor.YearsOfExperience.Should().Be(appointment.Doctor.YearsOfExperience);
        dto.Doctor.IsActive.Should().Be(appointment.Doctor.IsActive);
        dto.Doctor.IsAcceptingPatients.Should().Be(appointment.Doctor.IsAcceptingPatients);
    }

    [Fact]
    public async Task Handle_ShouldMapScheduledTimeCorrectly()
    {
        // Arrange
        var appointment = await CreateAndSaveAppointmentAsync();
        var query = new GetAppointmentQuery(appointment.Id);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        var dto = result.Value;
        dto.ScheduledTime.Should().Be(appointment.ScheduledTime.Value);
        dto.ScheduledDate.Should().NotBeNullOrEmpty();
        dto.ScheduledTimeFormatted.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_ShouldMapStatusCorrectly()
    {
        // Arrange
        var appointment = await CreateAndSaveAppointmentAsync(AppointmentStatus.Confirmed);
        var query = new GetAppointmentQuery(appointment.Id);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Value.Status.Should().Be(AppointmentStatus.Confirmed.ToString());
    }

    [Fact]
    public async Task Handle_ShouldMapConsultationFeeCorrectly()
    {
        // Arrange
        var appointment = await CreateAndSaveAppointmentAsync();
        var query = new GetAppointmentQuery(appointment.Id);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        var dto = result.Value;
        dto.ConsultationFeeAmount.Should().Be(appointment.ConsultationFee.Amount);
        dto.ConsultationFeeCurrency.Should().Be(appointment.ConsultationFee.Currency);
    }

    [Fact]
    public async Task Handle_WithConfirmedAppointment_ShouldMapConfirmedAt()
    {
        // Arrange
        var appointment = await CreateAndSaveAppointmentAsync(AppointmentStatus.Confirmed);
        var query = new GetAppointmentQuery(appointment.Id);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Value.ConfirmedAt.Should().NotBeNull();
        result.Value.ConfirmedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    #endregion

    #region Not Found Tests

    [Fact]
    public async Task Handle_WithNonExistentAppointment_ShouldReturnFailure()
    {
        // Arrange
        var query = new GetAppointmentQuery(9999); // Non-existent ID

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        result.Error.Should().Contain("9999");
    }

    #endregion

    #region Different Status Tests

    [Fact]
    public async Task Handle_WithPendingAppointment_ShouldMapCorrectly()
    {
        // Arrange
        var appointment = await CreateAndSaveAppointmentAsync(AppointmentStatus.Pending);
        var query = new GetAppointmentQuery(appointment.Id);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Pending");
        result.Value.ConfirmedAt.Should().BeNull();
        result.Value.CompletedAt.Should().BeNull();
        result.Value.CancelledAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithConfirmedAppointment_ShouldMapCorrectly()
    {
        // Arrange
        var appointment = await CreateAndSaveAppointmentAsync(AppointmentStatus.Confirmed);
        var query = new GetAppointmentQuery(appointment.Id);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Confirmed");
        result.Value.ConfirmedAt.Should().NotBeNull();
        result.Value.CompletedAt.Should().BeNull();
        result.Value.CancelledAt.Should().BeNull();
    }

    #endregion

    #region DTO Completeness Tests

    [Fact]
    public async Task Handle_ShouldMapAllRequiredDtoFields()
    {
        // Arrange
        var appointment = await CreateAndSaveAppointmentAsync(AppointmentStatus.Confirmed);
        var query = new GetAppointmentQuery(appointment.Id);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        var dto = result.Value;

        // Appointment fields
        dto.Id.Should().BeGreaterThan(0);
        dto.ScheduledTime.Should().NotBe(default);
        dto.Status.Should().NotBeNullOrEmpty();
        dto.Reason.Should().NotBeNullOrEmpty();
        dto.CreatedAt.Should().NotBe(default);

        // Patient fields
        dto.Patient.Should().NotBeNull();
        dto.Patient.FullName.Should().NotBeNullOrEmpty();
        dto.Patient.Email.Should().NotBeNullOrEmpty();
        dto.Patient.PhoneNumber.Should().NotBeNullOrEmpty();

        // Doctor fields
        dto.Doctor.Should().NotBeNull();
        dto.Doctor.FullName.Should().NotBeNullOrEmpty();
        dto.Doctor.Email.Should().NotBeNullOrEmpty();
        dto.Doctor.LicenseNumber.Should().NotBeNullOrEmpty();

        // Consultation fee
        dto.ConsultationFeeAmount.Should().BeGreaterThan(0);
        dto.ConsultationFeeCurrency.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Query Object Tests

    [Fact]
    public void Query_ShouldStoreAppointmentId()
    {
        // Arrange & Act
        var query = new GetAppointmentQuery(123);

        // Assert
        query.AppointmentId.Should().Be(123);
    }

    [Fact]
    public void Query_CanBeCreatedWithConstructor()
    {
        // Act
        var query = new GetAppointmentQuery(456);

        // Assert
        query.Should().NotBeNull();
        query.AppointmentId.Should().Be(456);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Handle_WithAppointmentId1_ShouldWork()
    {
        // Arrange
        var appointment = await CreateAndSaveAppointmentAsync();

        // Force ID to 1 (edge case - first ID)
        var idProperty = typeof(Domain.Entities.AppointmentTests)
            .GetProperty("Id");
        idProperty!.SetValue(appointment, 1);

        var query = new GetAppointmentQuery(1);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert - Should handle ID 1 correctly
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WithReasonContainingSpecialCharacters_ShouldMapCorrectly()
    {
        // Arrange
        var patient = CreateTestPatient();
        var doctor = CreateTestDoctor();
        await _patientRepository.AddAsync(patient);
        await _doctorRepository.AddAsync(doctor);

        var scheduledTime = CreateFutureAppointmentTime();
        const string specialReason = "Patient has symptoms: fever, cough & headache (urgent!)";

        var appointment = Appointment.Create(patient, doctor, scheduledTime, specialReason);
        await _appointmentRepository.AddAsync(appointment);

        var query = new GetAppointmentQuery(appointment.Id);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Value.Reason.Should().Be(specialReason);
    }

    #endregion
}