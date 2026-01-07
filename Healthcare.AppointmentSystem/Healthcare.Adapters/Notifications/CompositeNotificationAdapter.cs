using Healthcare.Application.Ports.Notifications;
using Healthcare.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Healthcare.Adapters.Notifications;

/// <summary>
/// Composite notification adapter that sends via multiple channels.
/// </summary>
/// <remarks>
/// Design Pattern: Composite Pattern + Strategy Pattern
/// 
/// This adapter:
/// - Combines multiple notification strategies
/// - Sends notifications through ALL registered channels
/// - Handles failures gracefully (one fails, others continue)
/// - Useful for redundancy (Email + SMS + Push)
/// 
/// Example Usage:
/// var composite = new CompositeNotificationAdapter(logger,
///     new EmailNotificationAdapter(...),
///     new SmsNotificationAdapter(...),
///     new PushNotificationAdapter(...)
/// );
/// 
/// Benefits:
/// - Redundancy: If email fails, SMS still works
/// - Flexibility: Easy to add/remove channels
/// - Testability: Can mix real and mock implementations
/// </remarks>
public sealed class CompositeNotificationAdapter : INotificationService
{
    private readonly ILogger<CompositeNotificationAdapter> _logger;
    private readonly IEnumerable<INotificationService> _notificationServices;

    /// <summary>
    /// Initializes a new instance with multiple notification services.
    /// </summary>
    public CompositeNotificationAdapter(
        ILogger<CompositeNotificationAdapter> logger,
        params INotificationService[] notificationServices)
    {
        _logger = logger;
        _notificationServices = notificationServices;

        if (!notificationServices.Any())
        {
            _logger.LogWarning("CompositeNotificationAdapter created with no services!");
        }
    }

    public async Task SendAppointmentConfirmationAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending appointment confirmation via {Count} channels for appointment {Id}",
            _notificationServices.Count(),
            appointment.Id);

        await SendToAllServicesAsync(
            service => service.SendAppointmentConfirmationAsync(appointment, cancellationToken),
            "AppointmentConfirmation",
            appointment.Id);
    }

    public async Task SendAppointmentReminderAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending appointment reminder via {Count} channels for appointment {Id}",
            _notificationServices.Count(),
            appointment.Id);

        await SendToAllServicesAsync(
            service => service.SendAppointmentReminderAsync(appointment, cancellationToken),
            "AppointmentReminder",
            appointment.Id);
    }

    public async Task SendAppointmentCancellationAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending appointment cancellation via {Count} channels for appointment {Id}",
            _notificationServices.Count(),
            appointment.Id);

        await SendToAllServicesAsync(
            service => service.SendAppointmentCancellationAsync(appointment, cancellationToken),
            "AppointmentCancellation",
            appointment.Id);
    }

    public async Task SendAppointmentRescheduledAsync(
        Appointment appointment,
        DateTime oldTime,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending appointment rescheduled via {Count} channels for appointment {Id}",
            _notificationServices.Count(),
            appointment.Id);

        await SendToAllServicesAsync(
            service => service.SendAppointmentRescheduledAsync(appointment, oldTime, cancellationToken),
            "AppointmentRescheduled",
            appointment.Id);
    }

    /// <summary>
    /// Sends notification to all services, handling failures gracefully.
    /// </summary>
    /// <remarks>
    /// Important: If one service fails, others continue.
    /// All errors are logged but not thrown.
    /// </remarks>
    private async Task SendToAllServicesAsync(
        Func<INotificationService, Task> sendAction,
        string notificationType,
        int appointmentId)
    {
        var tasks = _notificationServices.Select(async service =>
        {
            try
            {
                await sendAction(service);

                _logger.LogDebug(
                    "{NotificationType} sent successfully via {ServiceType} for appointment {Id}",
                    notificationType,
                    service.GetType().Name,
                    appointmentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send {NotificationType} via {ServiceType} for appointment {Id}",
                    notificationType,
                    service.GetType().Name,
                    appointmentId);

                // Don't throw - let other services continue
            }
        });

        await Task.WhenAll(tasks);

        _logger.LogInformation(
            "{NotificationType} completed for appointment {Id}",
            notificationType,
            appointmentId);
    }
}