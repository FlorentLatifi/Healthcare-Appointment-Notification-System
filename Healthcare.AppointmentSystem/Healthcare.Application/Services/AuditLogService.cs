using System.Text.Json;
using Healthcare.Application.Ports.Audit;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Healthcare.Application.Services;

/// <summary>
/// Append-only audit writer. Inserts only; repository has no update/delete APIs.
/// </summary>
public sealed class AuditLogService : IAuditLogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IAuditLogRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditContext _auditContext;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(
        IAuditLogRepository repository,
        IUnitOfWork unitOfWork,
        IAuditContext auditContext,
        ILogger<AuditLogService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _auditContext = auditContext;
        _logger = logger;
    }

    public async Task WriteAsync(
        string action,
        string resourceType,
        int? resourceId,
        AuditOutcome outcome,
        object? details = null,
        int? actorUserIdOverride = null,
        string? actorRoleOverride = null,
        bool throwOnFailure = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var detailsJson = details is null
                ? "{}"
                : details is string s
                    ? s
                    : JsonSerializer.Serialize(details, JsonOptions);

            var entry = AuditLogEntry.Create(
                action: action,
                resourceType: resourceType,
                resourceId: resourceId,
                outcome: outcome,
                actorUserId: actorUserIdOverride ?? _auditContext.ActorUserId,
                actorRole: actorRoleOverride ?? _auditContext.ActorRole,
                clientIp: _auditContext.ClientIp,
                correlationId: _auditContext.CorrelationId,
                userAgent: _auditContext.UserAgent,
                detailsJson: detailsJson);

            await _repository.AddAsync(entry, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to write immutable audit log Action={Action} Resource={ResourceType}/{ResourceId} Outcome={Outcome}",
                action,
                resourceType,
                resourceId,
                outcome);

            if (throwOnFailure)
                throw;
        }
    }
}
