using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Healthcare.Adapters.Events.Handlers;

/// <summary>
/// Logs appointment confirmation to audit trail.
/// </summary>
/// <remarks>
/// Design Pattern: Observer Pattern
/// 
/// This handler creates an audit log entry when appointment is confirmed.
/// In production, this would write to:
/// - Audit database table
/// - External logging service (Elasticsearch, Splunk)
/// - Compliance tracking system
/// 
/// Multiple handlers can observe the same event!
/// - SendConfirmationNotificationHandler sends email
/// - LogAppointmentConfirmedHandler writes audit log
/// - UpdateAnalyticsHandler updates statistics
/// </remarks>
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

        // In production, write to audit database or external logging service:
        // await _auditRepository.AddAsync(new AuditLog { ... });
        // await _elasticsearchClient.IndexAsync(new AuditDocument { ... });

        return Task.CompletedTask;
    }
}