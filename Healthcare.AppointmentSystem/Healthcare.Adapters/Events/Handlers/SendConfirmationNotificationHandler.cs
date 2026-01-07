using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Notifications;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Healthcare.Adapters.Events.Handlers;

/// <summary>
/// Sends confirmation notification when appointment is confirmed.
/// </summary>
/// <remarks>
/// Design Pattern: Observer Pattern
/// 
/// This handler OBSERVES AppointmentConfirmedEvent.
/// When the event is raised, this handler:
/// 1. Fetches the full appointment from repository
/// 2. Sends confirmation notification via INotificationService
/// 3. Logs the action
/// 
/// Error Handling:
/// - Catches all exceptions
/// - Logs errors but doesn't throw
/// - Notification failure shouldn't break the domain flow
/// </remarks>
public sealed class SendConfirmationNotificationHandler
    : IDomainEventHandler<AppointmentConfirmedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SendConfirmationNotificationHandler> _logger;

    public SendConfirmationNotificationHandler(
        INotificationService notificationService,
        IUnitOfWork unitOfWork,
        ILogger<SendConfirmationNotificationHandler> logger)
    {
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task HandleAsync(
        AppointmentConfirmedEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling AppointmentConfirmedEvent {EventId} for appointment {AppointmentId}",
            domainEvent.EventId,
            domainEvent.AppointmentId);

        try
        {
            // Fetch full appointment with navigation properties
            var appointment = await _unitOfWork.Appointments
                .GetByIdAsync(domainEvent.AppointmentId, cancellationToken);

            if (appointment == null)
            {
                _logger.LogWarning(
                    "Appointment {AppointmentId} not found for event {EventId}",
                    domainEvent.AppointmentId,
                    domainEvent.EventId);
                return;
            }

            // Send notification
            await _notificationService.SendAppointmentConfirmationAsync(
                appointment,
                cancellationToken);

            _logger.LogInformation(
                "Confirmation notification sent for appointment {AppointmentId}",
                domainEvent.AppointmentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send confirmation notification for appointment {AppointmentId}",
                domainEvent.AppointmentId);

            // Don't throw - notification failure shouldn't break the flow
        }
    }
}