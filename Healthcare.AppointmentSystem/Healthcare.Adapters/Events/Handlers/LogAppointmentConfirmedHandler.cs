using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Events;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Healthcare.Adapters.Events.Handlers;

public sealed class LogAppointmentConfirmedHandler
    : IDomainEventHandler<AppointmentConfirmedEvent>
{
    private readonly ILogger<LogAppointmentConfirmedHandler> _logger;
    private readonly IAuditLogRepository _auditLogRepo;
    private readonly IUnitOfWork _unitOfWork;

    public LogAppointmentConfirmedHandler(
        ILogger<LogAppointmentConfirmedHandler> logger,
        IAuditLogRepository auditLogRepo,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _auditLogRepo = auditLogRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
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

        var details = JsonSerializer.Serialize(new
        {
            domainEvent.AppointmentId,
            domainEvent.PatientId,
            domainEvent.DoctorId,
            domainEvent.ScheduledTime,
            PaymentOverrideReason = domainEvent.PaymentOverrideReason
        });

        var entry = new AuditLogEntry(
            "AppointmentConfirmed",
            "Appointment",
            domainEvent.AppointmentId,
            domainEvent.OccurredOn,
            details,
            null);

        await _auditLogRepo.AddAsync(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
