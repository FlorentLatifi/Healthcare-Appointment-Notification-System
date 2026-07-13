using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using System.Collections.Concurrent;

namespace Healthcare.Adapters.Persistence.InMemory;

public sealed class InMemoryAuditLogRepository : IAuditLogRepository
{
    private readonly ConcurrentBag<AuditLogEntry> _entries = new();
    private int _nextId = 1;

    public Task AddAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        // Assign identity for in-memory queries when EF is not present
        if (entry.Id == 0)
        {
            var id = Interlocked.Increment(ref _nextId);
            typeof(AuditLogEntry).BaseType!
                .GetProperty(nameof(AuditLogEntry.Id))!
                .SetValue(entry, id);
        }

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
        string? action = null,
        int? actorUserId = null,
        string? outcome = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var query = Filter(_entries, entityType, entityId, from, to, action, actorUserId, outcome, correlationId);

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
        string? action = null,
        int? actorUserId = null,
        string? outcome = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var query = Filter(_entries, entityType, entityId, from, to, action, actorUserId, outcome, correlationId);
        return Task.FromResult(query.Count());
    }

    private static IEnumerable<AuditLogEntry> Filter(
        IEnumerable<AuditLogEntry> source,
        string? entityType,
        int? entityId,
        DateTime? from,
        DateTime? to,
        string? action,
        int? actorUserId,
        string? outcome,
        string? correlationId)
    {
        var query = source;

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType == entityType);

        if (entityId.HasValue)
            query = query.Where(a => a.EntityId == entityId);

        if (from.HasValue)
            query = query.Where(a => a.OccurredOn >= from.Value);

        if (to.HasValue)
            query = query.Where(a => a.OccurredOn <= to.Value);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.EventType == action);

        if (actorUserId.HasValue)
            query = query.Where(a => a.UserId == actorUserId);

        if (!string.IsNullOrWhiteSpace(outcome))
            query = query.Where(a => a.Outcome == outcome);

        if (!string.IsNullOrWhiteSpace(correlationId))
            query = query.Where(a => a.CorrelationId == correlationId);

        return query;
    }
}
