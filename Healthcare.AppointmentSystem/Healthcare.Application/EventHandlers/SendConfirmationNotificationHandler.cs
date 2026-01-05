using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Notifications;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Events;

namespace Healthcare.Application.EventHandlers;

/// <summary>
/// Event handler that sends confirmation notifications when an appointment is confirmed.
/// </summary>
/// <remarks>
/// Design Pattern: Observer Pattern
/// 
/// This handler observes AppointmentConfirmedEvent and reacts by sending notifications.
/// Multiple handlers can observe the same event independently.
/// 
/// If this handler fails (e.g., email service is down), other handlers should still execute.
/// </remarks>
public sealed class SendConfirmationNotificationHandler
    : IDomainEventHandler<AppointmentConfirmedEvent>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly INotificationService _notificationService;

    public SendConfirmationNotificationHandler(
        IAppointmentRepository appointmentRepository,
        INotificationService notificationService)
    {
        _appointmentRepository = appointmentRepository;
        _notificationService = notificationService;
    }

    public async Task HandleAsync(
        AppointmentConfirmedEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Fetch full appointment details (with patient and doctor)
            var appointment = await _appointmentRepository
                .GetByIdAsync(domainEvent.AppointmentId, cancellationToken);

            if (appointment is null)
            {
                // Log warning but don't throw - event handlers should be resilient
                Console.WriteLine($"Warning: Appointment {domainEvent.AppointmentId} not found for notification.");
                return;
            }

            // 2. Send confirmation notification
            await _notificationService.SendAppointmentConfirmationAsync(appointment, cancellationToken);

            Console.WriteLine($"✅ Confirmation notification sent for appointment {appointment.Id}");
        }
        catch (Exception ex)
        {
            // Log error but don't throw - don't break other event handlers
            Console.WriteLine($"❌ Failed to send confirmation notification: {ex.Message}");
        }
    }
}