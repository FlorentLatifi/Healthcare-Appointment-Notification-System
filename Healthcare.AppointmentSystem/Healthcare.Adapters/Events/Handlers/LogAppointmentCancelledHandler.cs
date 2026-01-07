using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Healthcare.Adapters.Events.Handlers;

/// <summary>
/// Logs appointment cancellation to audit trail.
/// </summary>
public sealed class LogAppointmentCancelledHandler
    : IDomainEventHandler<AppointmentCancelledEvent>
{
    private readonly ILogger<LogAppointmentCancelledHandler> _logger;

    public LogAppointmentCancelledHandler(
        ILogger<LogAppointmentCancelledHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(
        AppointmentCancelledEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[AUDIT] Appointment {AppointmentId} cancelled at {Timestamp} | " +
            "Reason: {Reason} | Patient: {PatientId} | Doctor: {DoctorId}",
            domainEvent.AppointmentId,
            domainEvent.OccurredOn,
            domainEvent.CancellationReason,
            domainEvent.PatientId,
            domainEvent.DoctorId);

        return Task.CompletedTask;
    }
}