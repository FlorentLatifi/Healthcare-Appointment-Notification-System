using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Events;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Healthcare.Adapters.Events.Handlers;

public sealed class LogPatientRecordAccessedHandler
    : IDomainEventHandler<PatientRecordAccessedEvent>
{
    private readonly ILogger<LogPatientRecordAccessedHandler> _logger;
    private readonly IAuditLogRepository _auditLogRepo;
    private readonly IUnitOfWork _unitOfWork;

    public LogPatientRecordAccessedHandler(
        ILogger<LogPatientRecordAccessedHandler> logger,
        IAuditLogRepository auditLogRepo,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _auditLogRepo = auditLogRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
        PatientRecordAccessedEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[AUDIT] Patient record {PatientId} accessed by User {UserId} | {Description}",
            domainEvent.PatientId,
            domainEvent.AccessedByUserId,
            domainEvent.Description);

        var details = JsonSerializer.Serialize(new
        {
            domainEvent.PatientId,
            domainEvent.AccessedByUserId,
            domainEvent.Description
        });

        var entry = new AuditLogEntry(
            "PatientRecordAccessed",
            "Patient",
            domainEvent.PatientId,
            domainEvent.OccurredOn,
            details,
            domainEvent.AccessedByUserId);

        await _auditLogRepo.AddAsync(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
