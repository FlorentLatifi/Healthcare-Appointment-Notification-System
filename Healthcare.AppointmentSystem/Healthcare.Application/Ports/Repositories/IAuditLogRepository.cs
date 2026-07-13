using Healthcare.Domain.Entities;

namespace Healthcare.Application.Ports.Repositories;

/// <summary>
/// Append-only audit log store. Implementations must not expose update/delete.
/// </summary>
public interface IAuditLogRepository
{
    Task AddAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);

    Task<AuditLogEntry?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IEnumerable<AuditLogEntry>> QueryAsync(
        string? entityType = null,
        int? entityId = null,
        DateTime? from = null,
        DateTime? to = null,
        int pageNumber = 1,
        int pageSize = 20,
        string? action = null,
        int? actorUserId = null,
        string? outcome = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        string? entityType = null,
        int? entityId = null,
        DateTime? from = null,
        DateTime? to = null,
        string? action = null,
        int? actorUserId = null,
        string? outcome = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default);
}
