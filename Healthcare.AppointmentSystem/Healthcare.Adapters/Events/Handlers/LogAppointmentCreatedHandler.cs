using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Healthcare.Adapters.Events.Handlers;

/// <summary>
/// Logs appointment creation to audit trail.
/// </summary>
/// <remarks>
/// This handler logs when a new appointment is created (booked).
/// Useful for:
/// - Audit compliance
/// - Analytics (booking patterns)
/// - Monitoring system health
/// </remarks>
public sealed class LogAppointmentCreatedHandler
    : IDomainEventHandler<AppointmentCreatedEvent>
{
    private readonly ILogger<LogAppointmentCreatedHandler> _logger;

    public LogAppointmentCreatedHandler(
        ILogger<LogAppointmentCreatedHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(
        AppointmentCreatedEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[AUDIT] Appointment {AppointmentId} created at {Timestamp} | " +
            "Patient: {PatientId} | Doctor: {DoctorId} | Scheduled: {ScheduledTime}",
            domainEvent.AppointmentId,
            domainEvent.OccurredOn,
            domainEvent.PatientId,
            domainEvent.DoctorId,
            domainEvent.ScheduledTime);

        return Task.CompletedTask;
    }
}