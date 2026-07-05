using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Events;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Healthcare.Adapters.Events.Handlers;

public sealed class LogAppointmentCreatedHandler
    : IDomainEventHandler<AppointmentCreatedEvent>
{
    private readonly ILogger<LogAppointmentCreatedHandler> _logger;
    private readonly IAuditLogRepository _auditLogRepo;
    private readonly IUnitOfWork _unitOfWork;

    public LogAppointmentCreatedHandler(
        ILogger<LogAppointmentCreatedHandler> logger,
        IAuditLogRepository auditLogRepo,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _auditLogRepo = auditLogRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
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

        var details = JsonSerializer.Serialize(new
        {
            domainEvent.AppointmentId,
            domainEvent.PatientId,
            domainEvent.DoctorId,
            domainEvent.ScheduledTime
        });

        var entry = new AuditLogEntry(
            "AppointmentCreated",
            "Appointment",
            domainEvent.AppointmentId,
            domainEvent.OccurredOn,
            details,
            null);

        await _auditLogRepo.AddAsync(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
