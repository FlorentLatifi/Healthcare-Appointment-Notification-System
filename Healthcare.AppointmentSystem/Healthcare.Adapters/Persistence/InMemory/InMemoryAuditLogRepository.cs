using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using System.Collections.Concurrent;

namespace Healthcare.Adapters.Persistence.InMemory;

public sealed class InMemoryAuditLogRepository : IAuditLogRepository
{
    private readonly ConcurrentBag<AuditLogEntry> _entries = new();

    public Task AddAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        _entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<AuditLogEntry?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var result = _entries.FirstOrDefault(e => e.Id == id);
        return Task.FromResult(result);
    }

    public Task<IEnumerable<AuditLogEntry>> QueryAsync(
        string? entityType = null,
        int? entityId = null,
        DateTime? from = null,
        DateTime? to = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _entries.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType == entityType);

        if (entityId.HasValue)
            query = query.Where(a => a.EntityId == entityId);

        if (from.HasValue)
            query = query.Where(a => a.OccurredOn >= from.Value);

        if (to.HasValue)
            query = query.Where(a => a.OccurredOn <= to.Value);

        var result = query
            .OrderByDescending(a => a.OccurredOn)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult<IEnumerable<AuditLogEntry>>(result);
    }

    public Task<int> CountAsync(
        string? entityType = null,
        int? entityId = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var query = _entries.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType == entityType);

        if (entityId.HasValue)
            query = query.Where(a => a.EntityId == entityId);

        if (from.HasValue)
            query = query.Where(a => a.OccurredOn >= from.Value);

        if (to.HasValue)
            query = query.Where(a => a.OccurredOn <= to.Value);

        return Task.FromResult(query.Count());
    }
}
