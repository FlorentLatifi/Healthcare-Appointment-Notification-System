using FluentAssertions;
using Healthcare.Application.Ports.Notifications;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.Services;
using Healthcare.Domain.ValueObjects;
using Healthcare.Presentation.API.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Presentation.Services;

public class AppointmentReminderBackgroundServiceTests
{
    private readonly Mock<IAppointmentRepository> _appointmentRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly AppointmentReminderBackgroundService _service;

    public AppointmentReminderBackgroundServiceTests()
    {
        _appointmentRepoMock = new Mock<IAppointmentRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _unitOfWorkMock.Setup(u => u.Appointments).Returns(_appointmentRepoMock.Object);

        _notificationServiceMock = new Mock<INotificationService>();

        var serviceProvider = new ServiceCollection()
            .AddScoped(_ => _unitOfWorkMock.Object)
            .AddScoped(_ => _notificationServiceMock.Object)
            .BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var loggerMock = new Mock<ILogger<AppointmentReminderBackgroundService>>();
        var settings = Options.Create(new ReminderSettings());

        _service = new AppointmentReminderBackgroundService(
            scopeFactory, loggerMock.Object, settings);
    }

    [Fact]
    public async Task ProcessBatchAsync_WithAppointmentNeedingReminder_SendsNotificationAndMarksReminded()
    {
        var appointment = CreateConfirmedAppointment(EmailEnabled: true);
        _appointmentRepoMock.Setup(r => r.GetAppointmentsNeedingRemindersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { appointment });

        await _service.ProcessBatchAsync(CancellationToken.None);

        _notificationServiceMock.Verify(n => n.SendAppointmentReminderAsync(
            appointment, It.IsAny<CancellationToken>()), Times.Once);
        appointment.RemindedAt.Should().NotBeNull();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessBatchAsync_WithEmailDisabled_SkipsNotificationButStillMarksReminded()
    {
        var appointment = CreateConfirmedAppointment(EmailEnabled: false);
        _appointmentRepoMock.Setup(r => r.GetAppointmentsNeedingRemindersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { appointment });

        await _service.ProcessBatchAsync(CancellationToken.None);

        _notificationServiceMock.Verify(n => n.SendAppointmentReminderAsync(
            It.IsAny<Appointment>(), It.IsAny<CancellationToken>()), Times.Never);
        appointment.RemindedAt.Should().NotBeNull();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessBatchAsync_WithNoAppointmentsNeedingReminder_DoesNothing()
    {
        _appointmentRepoMock.Setup(r => r.GetAppointmentsNeedingRemindersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Appointment>());

        await _service.ProcessBatchAsync(CancellationToken.None);

        _notificationServiceMock.Verify(n => n.SendAppointmentReminderAsync(
            It.IsAny<Appointment>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenOneAppointmentFails_ContinuesProcessingOthers()
    {
        var goodAppointment = CreateConfirmedAppointment(EmailEnabled: true, id: 1);
        var badAppointment = CreateConfirmedAppointment(EmailEnabled: true, id: 2);

        _appointmentRepoMock.Setup(r => r.GetAppointmentsNeedingRemindersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { goodAppointment, badAppointment });

        _notificationServiceMock.Setup(n => n.SendAppointmentReminderAsync(
            badAppointment, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Send failed"));

        await _service.ProcessBatchAsync(CancellationToken.None);

        _notificationServiceMock.Verify(n => n.SendAppointmentReminderAsync(
            goodAppointment, It.IsAny<CancellationToken>()), Times.Once);
        _notificationServiceMock.Verify(n => n.SendAppointmentReminderAsync(
            badAppointment, It.IsAny<CancellationToken>()), Times.Once);
        goodAppointment.RemindedAt.Should().NotBeNull();
        badAppointment.RemindedAt.Should().BeNull();
    }

    private static Appointment CreateConfirmedAppointment(bool EmailEnabled, int id = 1)
    {
        var patient = Patient.Create(
            "John",
            "Doe",
            Email.Create("john@test.com"),
            PhoneNumber.Create("+355671234567"),
            new DateTime(1990, 1, 1),
            Gender.Male,
            Address.Create("123 Main St", "Pristina", "Kosovo", "10000", "Kosovo"));

        if (!EmailEnabled)
        {
            patient.UpdateNotificationPreferences(false, false);
        }

        var doctor = Doctor.Create(
            "Jane",
            "Smith",
            Email.Create("jane@test.com"),
            PhoneNumber.Create("+355679876543"),
            "LIC-12345",
            Money.Create(100m, "USD"),
            10,
            Specialty.Cardiology);

        var futureDate = DateTime.Now.AddDays(1).Date.AddHours(10);
        var appointmentTime = AppointmentTime.Create(futureDate);
        var appointment = Appointment.Create(
            patient, doctor, appointmentTime,
            "Annual checkup and consultation",
            AppointmentCodeGenerator.Instance);

        appointment.Confirm();

        var prop = typeof(Healthcare.Domain.Common.Entity).GetProperty("Id",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        prop?.SetValue(appointment, id);

        return appointment;
    }
}
