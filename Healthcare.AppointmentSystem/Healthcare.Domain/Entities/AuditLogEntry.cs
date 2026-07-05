using Healthcare.Domain.Common;

namespace Healthcare.Domain.Entities;

public sealed class AuditLogEntry : Entity
{
    public string EventType { get; private set; }
    public string EntityType { get; private set; }
    public int? EntityId { get; private set; }
    public DateTime OccurredOn { get; private set; }
    public string Details { get; private set; }
    public int? UserId { get; private set; }

    private AuditLogEntry() { }

    public AuditLogEntry(string eventType, string entityType, int? entityId, DateTime occurredOn, string details, int? userId)
    {
        EventType = eventType;
        EntityType = entityType;
        EntityId = entityId;
        OccurredOn = occurredOn;
        Details = details;
        UserId = userId;
        CreatedAt = DateTime.UtcNow;
    }
}
