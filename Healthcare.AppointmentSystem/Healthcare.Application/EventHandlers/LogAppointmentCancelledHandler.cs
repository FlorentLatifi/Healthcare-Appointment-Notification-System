using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Events;

namespace Healthcare.Application.EventHandlers;

/// <summary>
/// Event handler that logs when an appointment is cancelled.
/// </summary>
public sealed class LogAppointmentCancelledHandler
    : IDomainEventHandler<AppointmentCancelledEvent>
{
    public Task HandleAsync(
        AppointmentCancelledEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine("❌ APPOINTMENT CANCELLED - AUDIT LOG");
        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine($"Event ID:            {domainEvent.EventId}");
        Console.WriteLine($"Occurred On:         {domainEvent.OccurredOn:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"Appointment ID:      {domainEvent.AppointmentId}");
        Console.WriteLine($"Patient ID:          {domainEvent.PatientId}");
        Console.WriteLine($"Doctor ID:           {domainEvent.DoctorId}");
        Console.WriteLine($"Scheduled Time:      {domainEvent.ScheduledTime:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Cancellation Reason: {domainEvent.CancellationReason}");
        Console.WriteLine("═══════════════════════════════════════════════");

        return Task.CompletedTask;
    }
}
