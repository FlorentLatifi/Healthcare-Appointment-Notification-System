using Healthcare.Domain.Enums;

namespace Healthcare.Application.Ports.Audit;

/// <summary>
/// Append-only audit writer. Never updates or deletes records.
/// Failures are logged but must not break the primary business operation
/// when <paramref name="throwOnFailure"/> is false (default).
/// </summary>
public interface IAuditLogService
{
    Task WriteAsync(
        string action,
        string resourceType,
        int? resourceId,
        AuditOutcome outcome,
        object? details = null,
        int? actorUserIdOverride = null,
        string? actorRoleOverride = null,
        bool throwOnFailure = false,
        CancellationToken cancellationToken = default);
}
