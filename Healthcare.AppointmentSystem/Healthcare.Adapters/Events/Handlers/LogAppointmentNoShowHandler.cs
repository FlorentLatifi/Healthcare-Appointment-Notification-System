using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Events;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Healthcare.Adapters.Events.Handlers;

public sealed class LogAppointmentNoShowHandler
    : IDomainEventHandler<AppointmentNoShowEvent>
{
    private readonly ILogger<LogAppointmentNoShowHandler> _logger;
    private readonly IAuditLogRepository _auditLogRepo;
    private readonly IUnitOfWork _unitOfWork;

    public LogAppointmentNoShowHandler(
        ILogger<LogAppointmentNoShowHandler> logger,
        IAuditLogRepository auditLogRepo,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _auditLogRepo = auditLogRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
        AppointmentNoShowEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[AUDIT] Appointment {AppointmentId} marked as no-show at {Timestamp} | " +
            "Patient: {PatientId} | Doctor: {DoctorId} | Scheduled: {ScheduledTime}",
            domainEvent.AppointmentId,
            domainEvent.OccurredOn,
            domainEvent.PatientId,
            domainEvent.DoctorId,
            domainEvent.ScheduledTime);

        var details = JsonSerializer.Serialize(new
        {
            domainEvent.AppointmentId,
            domainEvent.PatientId,
            domainEvent.DoctorId,
            domainEvent.ScheduledTime
        });

        var entry = new AuditLogEntry(
            "AppointmentNoShow",
            "Appointment",
            domainEvent.AppointmentId,
            domainEvent.OccurredOn,
            details,
            null);

        await _auditLogRepo.AddAsync(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
