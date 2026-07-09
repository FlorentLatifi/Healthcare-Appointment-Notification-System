using FluentAssertions;
using Healthcare.Adapters.Events;
using Healthcare.Adapters.Persistence.InMemory;
using Healthcare.Application.Commands.MarkNoShowAppointment;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Repositories;
using System.Reflection;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Adapters.Services;
using Healthcare.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Application.Commands;

public class MarkNoShowAppointmentHandlerTests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly MarkNoShowAppointmentHandler _handler;

    public MarkNoShowAppointmentHandlerTests()
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

        _handler = new MarkNoShowAppointmentHandler(_unitOfWork, _eventDispatcher);
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

    private static AppointmentTime CreatePastAppointmentTime()
    {
        var pastDate = DateTime.UtcNow.AddDays(-1).Date.AddHours(10);
        var ctor = typeof(AppointmentTime).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[] { typeof(DateTime) },
            null);
        return (AppointmentTime)ctor!.Invoke(new object[] { pastDate });
    }

    private static void ForceSetScheduledTime(Appointment appointment, AppointmentTime pastTime)
    {
        var field = typeof(Appointment).GetField(
            "<ScheduledTime>k__BackingField",
            BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(appointment, pastTime);
    }

    private async Task<Appointment> CreateAndSaveConfirmedAppointmentAsync(bool usePastTime = false)
    {
        var patient = CreateTestPatient();
        var doctor = CreateTestDoctor();

        await _unitOfWork.Patients.AddAsync(patient);
        await _unitOfWork.Doctors.AddAsync(doctor);
        await _unitOfWork.SaveChangesAsync();

        var appointmentTime = CreateFutureAppointmentTime();

        var appointment = Appointment.Create(
            patient,
            doctor,
            appointmentTime,
            "Annual checkup and consultation",
            AppointmentCodeGenerator.Instance);

        appointment.Confirm();

        if (usePastTime)
        {
            ForceSetScheduledTime(appointment, CreatePastAppointmentTime());
        }

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

    #region Successful No-Show Tests

    [Fact]
    public async Task Handle_WithPastConfirmedAppointment_ShouldMarkAsNoShowSuccessfully()
    {
        var appointment = await CreateAndSaveConfirmedAppointmentAsync(usePastTime: true);

        var command = new MarkNoShowAppointmentCommand
        {
            AppointmentId = appointment.Id
        };

        var result = await _handler.HandleAsync(command);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        var noShowAppointment = await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);
        noShowAppointment.Should().NotBeNull();
        noShowAppointment!.Status.Should().Be(AppointmentStatus.NoShow);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task Handle_WithNonExistentAppointment_ShouldReturnFailure()
    {
        var command = new MarkNoShowAppointmentCommand
        {
            AppointmentId = 9999
        };

        var result = await _handler.HandleAsync(command);

        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        result.Error.Should().Contain("9999");
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

        var appointmentTime = CreateFutureAppointmentTime();
        var appointment = Appointment.Create(
            patient,
            doctor,
            appointmentTime,
            "Routine checkup and blood work",
            AppointmentCodeGenerator.Instance);

        appointment.ClearDomainEvents();

        await _unitOfWork.Appointments.AddAsync(appointment);
        await _unitOfWork.SaveChangesAsync();

        var command = new MarkNoShowAppointmentCommand
        {
            AppointmentId = appointment.Id
        };

        var result = await _handler.HandleAsync(command);

        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("no-show");
        result.Error.Should().Contain("Pending");
    }

    [Fact]
    public async Task Handle_WithCompletedAppointment_ShouldReturnFailure()
    {
        var appointment = await CreateAndSaveConfirmedAppointmentAsync();

        appointment.Complete("Examination completed successfully with no issues found.");
        await _unitOfWork.Appointments.UpdateAsync(appointment);
        await _unitOfWork.SaveChangesAsync();

        var command = new MarkNoShowAppointmentCommand
        {
            AppointmentId = appointment.Id
        };

        var result = await _handler.HandleAsync(command);

        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Completed");
    }

    [Fact]
    public async Task Handle_WithAlreadyNoShowAppointment_ShouldReturnFailure()
    {
        var appointment = await CreateAndSaveConfirmedAppointmentAsync(usePastTime: true);

        appointment.MarkAsNoShow();
        await _unitOfWork.Appointments.UpdateAsync(appointment);
        await _unitOfWork.SaveChangesAsync();

        var command = new MarkNoShowAppointmentCommand
        {
            AppointmentId = appointment.Id
        };

        var result = await _handler.HandleAsync(command);

        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("NoShow");
    }

    #endregion

    #region Business Rule Tests

    [Fact]
    public async Task Handle_WithFutureAppointment_ShouldReturnFailure()
    {
        var appointment = await CreateAndSaveConfirmedAppointmentAsync();

        var command = new MarkNoShowAppointmentCommand
        {
            AppointmentId = appointment.Id
        };

        var result = await _handler.HandleAsync(command);

        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("before the scheduled time");
    }

    #endregion

    #region Domain Events Tests

    [Fact]
    public async Task Handle_ShouldClearDomainEventsAfterDispatching()
    {
        var appointment = await CreateAndSaveConfirmedAppointmentAsync(usePastTime: true);

        var command = new MarkNoShowAppointmentCommand
        {
            AppointmentId = appointment.Id
        };

        await _handler.HandleAsync(command);

        var updatedAppointment = await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);
        updatedAppointment!.DomainEvents.Should().BeEmpty();
    }

    #endregion
}
