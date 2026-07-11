using System.Text.Json;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;

namespace Healthcare.Presentation.API.Security;

/// <summary>
/// Writes security-relevant events (auth, access control) to the durable audit log.
/// Never include passwords, tokens, or full secrets in the details payload.
/// </summary>
public sealed class SecurityAuditWriter
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SecurityAuditWriter> _logger;

    public SecurityAuditWriter(
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        ILogger<SecurityAuditWriter> logger)
    {
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task WriteAsync(
        string eventType,
        string entityType,
        int? entityId,
        int? actorUserId,
        object details,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(details);
            var entry = new AuditLogEntry(
                eventType,
                entityType,
                entityId,
                DateTime.UtcNow,
                json,
                actorUserId);

            await _auditLogRepository.AddAsync(entry, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Audit failure must not block auth flows; log loudly for ops.
            _logger.LogError(ex, "Failed to persist security audit event {EventType}", eventType);
        }
    }
}
