using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Notifications;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Healthcare.Adapters.Events.Handlers;

/// <summary>
/// Sends cancellation notification when appointment is cancelled.
/// </summary>
public sealed class SendCancellationNotificationHandler
    : IDomainEventHandler<AppointmentCancelledEvent>
{
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SendCancellationNotificationHandler> _logger;

    public SendCancellationNotificationHandler(
        INotificationService notificationService,
        IUnitOfWork unitOfWork,
        ILogger<SendCancellationNotificationHandler> logger)
    {
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task HandleAsync(
        AppointmentCancelledEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling AppointmentCancelledEvent {EventId} for appointment {AppointmentId}",
            domainEvent.EventId,
            domainEvent.AppointmentId);

        try
        {
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

            await _notificationService.SendAppointmentCancellationAsync(
                appointment,
                cancellationToken);

            _logger.LogInformation(
                "Cancellation notification sent for appointment {AppointmentId}",
                domainEvent.AppointmentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send cancellation notification for appointment {AppointmentId}",
                domainEvent.AppointmentId);
        }
    }
}