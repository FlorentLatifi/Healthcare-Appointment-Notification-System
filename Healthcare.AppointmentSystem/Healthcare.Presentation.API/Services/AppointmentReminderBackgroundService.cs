using Healthcare.Application.Ports.Notifications;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Healthcare.Presentation.API.Services;

public sealed class AppointmentReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AppointmentReminderBackgroundService> _logger;
    private readonly ReminderSettings _settings;

    public AppointmentReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<AppointmentReminderBackgroundService> logger,
        IOptions<ReminderSettings> settings)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Appointment reminder background service started (interval: {IntervalMinutes} min)",
            _settings.IntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing appointment reminder batch");
            }

            await Task.Delay(
                TimeSpan.FromMinutes(_settings.IntervalMinutes),
                stoppingToken);
        }
    }

    public async Task ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var appointments = (await unitOfWork.Appointments
            .GetAppointmentsNeedingRemindersAsync(cancellationToken))
            .ToList();

        _logger.LogInformation(
            "Found {Count} appointment(s) needing reminders", appointments.Count);

        foreach (var appointment in appointments)
        {
            await ProcessAppointmentAsync(appointment, unitOfWork, notificationService, cancellationToken);
        }

        if (appointments.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ProcessAppointmentAsync(
        Appointment appointment,
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        try
        {
            var prefs = appointment.Patient?.NotificationPreferences;
            if (prefs is { EmailEnabled: false })
            {
                _logger.LogInformation(
                    "Email notifications disabled for patient {PatientId}, skipping reminder for appointment {AppointmentId}",
                    appointment.PatientId, appointment.Id);
            }
            else
            {
                await notificationService.SendAppointmentReminderAsync(appointment, cancellationToken);
                _logger.LogInformation(
                    "Reminder sent for appointment {AppointmentId}", appointment.Id);
            }

            appointment.MarkReminderSent();
            await unitOfWork.Appointments.UpdateAsync(appointment, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process reminder for appointment {AppointmentId}",
                appointment.Id);
        }
    }
}
