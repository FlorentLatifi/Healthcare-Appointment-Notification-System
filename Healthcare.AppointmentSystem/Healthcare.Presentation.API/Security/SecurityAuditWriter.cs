using Healthcare.Application.Ports.Audit;
using Healthcare.Domain.Enums;

namespace Healthcare.Presentation.API.Security;

/// <summary>
/// Writes security-relevant events (auth, access control) to the durable audit log.
/// Never include passwords, tokens, or full secrets in the details payload.
/// Delegates to <see cref="IAuditLogService"/> (append-only).
/// </summary>
public sealed class SecurityAuditWriter
{
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<SecurityAuditWriter> _logger;

    public SecurityAuditWriter(
        IAuditLogService auditLogService,
        ILogger<SecurityAuditWriter> logger)
    {
        _auditLogService = auditLogService;
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
            await _auditLogService.WriteAsync(
                action: eventType,
                resourceType: entityType,
                resourceId: entityId,
                outcome: AuditOutcome.Success,
                details: details,
                actorUserIdOverride: actorUserId,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            // Audit failure must not block auth flows; log loudly for ops.
            _logger.LogError(ex, "Failed to persist security audit event {EventType}", eventType);
        }
    }
}
