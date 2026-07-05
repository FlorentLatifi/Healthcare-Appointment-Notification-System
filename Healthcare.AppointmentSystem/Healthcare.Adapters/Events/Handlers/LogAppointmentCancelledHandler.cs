using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Events;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Healthcare.Adapters.Events.Handlers;

public sealed class LogAppointmentCancelledHandler
    : IDomainEventHandler<AppointmentCancelledEvent>
{
    private readonly ILogger<LogAppointmentCancelledHandler> _logger;
    private readonly IAuditLogRepository _auditLogRepo;
    private readonly IUnitOfWork _unitOfWork;

    public LogAppointmentCancelledHandler(
        ILogger<LogAppointmentCancelledHandler> logger,
        IAuditLogRepository auditLogRepo,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _auditLogRepo = auditLogRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
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

        var details = JsonSerializer.Serialize(new
        {
            domainEvent.AppointmentId,
            domainEvent.PatientId,
            domainEvent.DoctorId,
            domainEvent.ScheduledTime,
            domainEvent.CancellationReason
        });

        var entry = new AuditLogEntry(
            "AppointmentCancelled",
            "Appointment",
            domainEvent.AppointmentId,
            domainEvent.OccurredOn,
            details,
            null);

        await _auditLogRepo.AddAsync(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
