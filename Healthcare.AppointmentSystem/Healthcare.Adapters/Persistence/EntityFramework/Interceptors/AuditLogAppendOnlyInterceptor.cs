using Healthcare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Healthcare.Adapters.Persistence.EntityFramework.Interceptors;

/// <summary>
/// Enforces append-only semantics for <see cref="AuditLogEntry"/> at the DbContext boundary.
/// Application repositories expose only Add/Query; this rejects accidental Modified/Deleted states.
/// </summary>
public sealed class AuditLogAppendOnlyInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        RejectNonInserts(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        RejectNonInserts(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void RejectNonInserts(DbContext? context)
    {
        if (context is null) return;

        foreach (var entry in context.ChangeTracker.Entries<AuditLogEntry>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "Audit logs are immutable (append-only). Updates and deletes are not permitted.");
            }
        }
    }
}
