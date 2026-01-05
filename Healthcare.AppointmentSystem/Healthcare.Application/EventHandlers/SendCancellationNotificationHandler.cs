using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Notifications;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Events;

namespace Healthcare.Application.EventHandlers;

/// <summary>
/// Event handler that sends cancellation notifications when an appointment is cancelled.
/// </summary>
public sealed class SendCancellationNotificationHandler
    : IDomainEventHandler<AppointmentCancelledEvent>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly INotificationService _notificationService;

    public SendCancellationNotificationHandler(
        IAppointmentRepository appointmentRepository,
        INotificationService notificationService)
    {
        _appointmentRepository = appointmentRepository;
        _notificationService = notificationService;
    }

    public async Task HandleAsync(
        AppointmentCancelledEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var appointment = await _appointmentRepository
                .GetByIdAsync(domainEvent.AppointmentId, cancellationToken);

            if (appointment is null)
            {
                Console.WriteLine($"Warning: Appointment {domainEvent.AppointmentId} not found for cancellation notification.");
                return;
            }

            await _notificationService.SendAppointmentCancellationAsync(appointment, cancellationToken);

            Console.WriteLine($"✅ Cancellation notification sent for appointment {appointment.Id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to send cancellation notification: {ex.Message}");
        }
    }
}