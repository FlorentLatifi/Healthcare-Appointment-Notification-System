using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.Adapters.Persistence.EntityFramework.Repositories;

public sealed class EFCoreAuditLogRepository : IAuditLogRepository
{
    private readonly HealthcareDbContext _context;

    public EFCoreAuditLogRepository(HealthcareDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        await _context.AuditLogs.AddAsync(entry, cancellationToken);
    }

    public async Task<AuditLogEntry?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<AuditLogEntry>> QueryAsync(
        string? entityType = null,
        int? entityId = null,
        DateTime? from = null,
        DateTime? to = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType == entityType);

        if (entityId.HasValue)
            query = query.Where(a => a.EntityId == entityId);

        if (from.HasValue)
            query = query.Where(a => a.OccurredOn >= from.Value);

        if (to.HasValue)
            query = query.Where(a => a.OccurredOn <= to.Value);

        return await query
            .OrderByDescending(a => a.OccurredOn)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(
        string? entityType = null,
        int? entityId = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType == entityType);

        if (entityId.HasValue)
            query = query.Where(a => a.EntityId == entityId);

        if (from.HasValue)
            query = query.Where(a => a.OccurredOn >= from.Value);

        if (to.HasValue)
            query = query.Where(a => a.OccurredOn <= to.Value);

        return await query.CountAsync(cancellationToken);
    }
}
