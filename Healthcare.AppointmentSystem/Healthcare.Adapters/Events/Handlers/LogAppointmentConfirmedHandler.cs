using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Healthcare.Adapters.Events.Handlers;

/// <summary>
/// Logs appointment confirmation to audit trail.
/// </summary>
public sealed class LogAppointmentConfirmedHandler
    : IDomainEventHandler<AppointmentConfirmedEvent>
{
    private readonly ILogger<LogAppointmentConfirmedHandler> _logger;

    public LogAppointmentConfirmedHandler(
        ILogger<LogAppointmentConfirmedHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(
        AppointmentConfirmedEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[AUDIT] Appointment {AppointmentId} confirmed at {Timestamp} | " +
            "Patient: {PatientId} | Doctor: {DoctorId} | Time: {ScheduledTime}",
            domainEvent.AppointmentId,
            domainEvent.OccurredOn,
            domainEvent.PatientId,
            domainEvent.DoctorId,
            domainEvent.ScheduledTime);

        if (!string.IsNullOrWhiteSpace(domainEvent.PaymentOverrideReason))
        {
            _logger.LogWarning(
                "[AUDIT] Appointment {AppointmentId} was confirmed WITHOUT a completed payment " +
                "(Doctor/Admin override). Reason: {OverrideReason}",
                domainEvent.AppointmentId,
                domainEvent.PaymentOverrideReason);
        }

        return Task.CompletedTask;
    }
}