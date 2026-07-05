using Healthcare.Domain.Entities;

namespace Healthcare.Application.Ports.Repositories;

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
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        string? entityType = null,
        int? entityId = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);
}
