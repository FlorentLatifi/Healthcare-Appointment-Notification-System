using FluentAssertions;
using Healthcare.Adapters.Events;
using Healthcare.Adapters.Persistence.InMemory;
using Healthcare.Application.Commands.CompleteAppointment;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Adapters.Services;
using Healthcare.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Application.Commands;

public class CompleteAppointmentHandlerTests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly CompleteAppointmentHandler _handler;

    public CompleteAppointmentHandlerTests()
    {
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
            auditLogRepo,
            Mock.Of<IUserSessionRepository>());

        var mockLogger = new Mock<ILogger<DomainEventDispatcher>>();
        var serviceProvider = CreateServiceProvider();
        _eventDispatcher = new DomainEventDispatcher(serviceProvider, mockLogger.Object);

        _handler = new CompleteAppointmentHandler(_unitOfWork, _eventDispatcher);
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

    private async Task<Appointment> CreateAndSaveConfirmedAppointmentAsync()
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
            "Annual checkup and consultation",
            AppointmentCodeGenerator.Instance);

        appointment.Confirm();
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

    #region Successful Completion Tests

    [Fact]
    public async Task Handle_WithConfirmedAppointment_ShouldCompleteSuccessfully()
    {
        var appointment = await CreateAndSaveConfirmedAppointmentAsync();

        var command = new CompleteAppointmentCommand
        {
            AppointmentId = appointment.Id,
            DoctorNotes = "Examination completed successfully with no issues found."
        };

        var result = await _handler.HandleAsync(command);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        var completedAppointment = await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);
        completedAppointment.Should().NotBeNull();
        completedAppointment!.Status.Should().Be(AppointmentStatus.Completed);
        completedAppointment.DoctorNotes.Should().Be(command.DoctorNotes);
        completedAppointment.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ShouldPersistDoctorNotes()
    {
        var appointment = await CreateAndSaveConfirmedAppointmentAsync();

        const string doctorNotes = "Patient showed good progress. Blood pressure normal. Follow-up in 3 months.";
        var command = new CompleteAppointmentCommand
        {
            AppointmentId = appointment.Id,
            DoctorNotes = doctorNotes
        };

        await _handler.HandleAsync(command);

        var updatedAppointment = await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);
        updatedAppointment!.DoctorNotes.Should().Be(doctorNotes);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task Handle_WithNonExistentAppointment_ShouldReturnFailure()
    {
        var command = new CompleteAppointmentCommand
        {
            AppointmentId = 9999,
            DoctorNotes = "Standard checkup completed with good results overall."
        };

        var result = await _handler.HandleAsync(command);

        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        result.Error.Should().Contain("9999");
    }

    [Theory]
    [InlineData("Short notes")] // Too short
    [InlineData("1234567890123456789")] // 19 characters
    public async Task Handle_WithTooShortDoctorNotes_ShouldReturnFailure(string shortNotes)
    {
        var appointment = await CreateAndSaveConfirmedAppointmentAsync();

        var command = new CompleteAppointmentCommand
        {
            AppointmentId = appointment.Id,
            DoctorNotes = shortNotes
        };

        var result = await _handler.HandleAsync(command);

        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("at least 20 characters");
    }

    #endregion

    #region Invalid State Tests

    [Fact]
    public async Task Handle_WithPendingAppointment_ShouldReturnFailure()
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
            "Routine checkup and blood work",
            AppointmentCodeGenerator.Instance);

        appointment.ClearDomainEvents();

        await _unitOfWork.Appointments.AddAsync(appointment);
        await _unitOfWork.SaveChangesAsync();

        var command = new CompleteAppointmentCommand
        {
            AppointmentId = appointment.Id,
            DoctorNotes = "Trying to complete a pending appointment with sufficient notes."
        };

        var result = await _handler.HandleAsync(command);

        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("complete");
        result.Error.Should().Contain("Pending");
    }

    [Fact]
    public async Task Handle_WithAlreadyCompletedAppointment_ShouldReturnFailure()
    {
        var appointment = await CreateAndSaveConfirmedAppointmentAsync();

        appointment.Complete("Examination completed successfully with no issues found.");
        await _unitOfWork.Appointments.UpdateAsync(appointment);
        await _unitOfWork.SaveChangesAsync();

        var command = new CompleteAppointmentCommand
        {
            AppointmentId = appointment.Id,
            DoctorNotes = "Trying to complete an already completed appointment."
        };

        var result = await _handler.HandleAsync(command);

        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Completed");
    }

    [Fact]
    public async Task Handle_WithCancelledAppointment_ShouldReturnFailure()
    {
        var appointment = await CreateAndSaveConfirmedAppointmentAsync();

        appointment.Cancel("Patient requested cancellation due to scheduling conflict");
        await _unitOfWork.Appointments.UpdateAsync(appointment);
        await _unitOfWork.SaveChangesAsync();

        var command = new CompleteAppointmentCommand
        {
            AppointmentId = appointment.Id,
            DoctorNotes = "Trying to complete a cancelled appointment with notes."
        };

        var result = await _handler.HandleAsync(command);

        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Cancelled");
    }

    #endregion

    #region Domain Events Tests

    [Fact]
    public async Task Handle_ShouldClearDomainEventsAfterDispatching()
    {
        var appointment = await CreateAndSaveConfirmedAppointmentAsync();

        var command = new CompleteAppointmentCommand
        {
            AppointmentId = appointment.Id,
            DoctorNotes = "Patient responded well to treatment. Prescribed medication for 2 weeks."
        };

        await _handler.HandleAsync(command);

        var updatedAppointment = await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);
        updatedAppointment!.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Handle_WithExactly20CharacterNotes_ShouldSucceed()
    {
        var appointment = await CreateAndSaveConfirmedAppointmentAsync();

        var command = new CompleteAppointmentCommand
        {
            AppointmentId = appointment.Id,
            DoctorNotes = "12345678901234567890" // Exactly 20 characters
        };

        var result = await _handler.HandleAsync(command);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithVeryLongNotes_ShouldSucceed()
    {
        var appointment = await CreateAndSaveConfirmedAppointmentAsync();

        var longNotes = new string('a', 1000);
        var command = new CompleteAppointmentCommand
        {
            AppointmentId = appointment.Id,
            DoctorNotes = longNotes
        };

        var result = await _handler.HandleAsync(command);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        var updatedAppointment = await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);
        updatedAppointment!.DoctorNotes.Should().HaveLength(1000);
    }

    #endregion
}
