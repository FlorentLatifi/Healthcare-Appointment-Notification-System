using Healthcare.Domain.Common;
using Healthcare.Domain.Enums;

namespace Healthcare.Domain.Entities;

/// <summary>
/// Immutable security / PHI access audit record.
/// Application code must only insert rows; updates and deletes are rejected.
/// </summary>
public sealed class AuditLogEntry : Entity
{
    /// <summary>Action / event name (e.g. BookAppointment, GetPatientById).</summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>Resource type (e.g. Patient, Appointment, Payment).</summary>
    public string EntityType { get; private set; } = string.Empty;

    /// <summary>Resource id when known.</summary>
    public int? EntityId { get; private set; }

    public DateTime OccurredOn { get; private set; }

    /// <summary>JSON details (no passwords, tokens, or full PHI free-text when avoidable).</summary>
    public string Details { get; private set; } = string.Empty;

    /// <summary>Actor user id (JWT subject) when authenticated.</summary>
    public int? UserId { get; private set; }

    public string? ActorRole { get; private set; }

    /// <summary>Success or Failure.</summary>
    public string Outcome { get; private set; } = AuditOutcome.Success.ToString();

    public string? ClientIp { get; private set; }

    public string? CorrelationId { get; private set; }

    public string? UserAgent { get; private set; }

    // Convenience aliases for compliance vocabulary
    public string Action => EventType;
    public string ResourceType => EntityType;
    public int? ResourceId => EntityId;
    public int? ActorUserId => UserId;

    private AuditLogEntry() { }

    /// <summary>Legacy constructor used by existing event handlers — outcome Success.</summary>
    public AuditLogEntry(
        string eventType,
        string entityType,
        int? entityId,
        DateTime occurredOn,
        string details,
        int? userId)
        : this(
            eventType,
            entityType,
            entityId,
            occurredOn,
            details,
            userId,
            actorRole: null,
            outcome: AuditOutcome.Success,
            clientIp: null,
            correlationId: null,
            userAgent: null)
    {
    }

    public AuditLogEntry(
        string eventType,
        string entityType,
        int? entityId,
        DateTime occurredOn,
        string details,
        int? userId,
        string? actorRole,
        AuditOutcome outcome,
        string? clientIp,
        string? correlationId,
        string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("Event type (action) is required.", nameof(eventType));
        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("Entity type (resource type) is required.", nameof(entityType));

        EventType = eventType.Trim();
        EntityType = entityType.Trim();
        EntityId = entityId;
        OccurredOn = occurredOn.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(occurredOn, DateTimeKind.Utc)
            : occurredOn.ToUniversalTime();
        Details = details ?? string.Empty;
        UserId = userId;
        ActorRole = string.IsNullOrWhiteSpace(actorRole) ? null : actorRole.Trim();
        Outcome = outcome.ToString();
        ClientIp = string.IsNullOrWhiteSpace(clientIp) ? null : clientIp.Trim();
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim();
        UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : Truncate(userAgent.Trim(), 512);
        CreatedAt = DateTime.UtcNow;
    }

    public static AuditLogEntry Create(
        string action,
        string resourceType,
        int? resourceId,
        AuditOutcome outcome,
        int? actorUserId,
        string? actorRole,
        string? clientIp,
        string? correlationId,
        string? userAgent,
        string detailsJson,
        DateTime? occurredOnUtc = null)
        => new(
            action,
            resourceType,
            resourceId,
            occurredOnUtc ?? DateTime.UtcNow,
            detailsJson,
            actorUserId,
            actorRole,
            outcome,
            clientIp,
            correlationId,
            userAgent);

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}
