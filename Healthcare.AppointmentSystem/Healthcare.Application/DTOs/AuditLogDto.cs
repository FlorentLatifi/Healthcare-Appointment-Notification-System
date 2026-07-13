namespace Healthcare.Application.DTOs;

/// <summary>
/// Audit log projection for admin API. Includes compliance field names
/// (Action / ResourceType / ActorUserId) as aliases of EventType / EntityType / UserId.
/// </summary>
public sealed class AuditLogDto
{
    public int Id { get; set; }

    /// <summary>Canonical action (e.g. BookAppointment).</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Legacy alias of <see cref="Action"/>.</summary>
    public string EventType { get; set; } = string.Empty;

    public string ResourceType { get; set; } = string.Empty;

    /// <summary>Legacy alias of <see cref="ResourceType"/>.</summary>
    public string EntityType { get; set; } = string.Empty;

    public int? ResourceId { get; set; }

    /// <summary>Legacy alias of <see cref="ResourceId"/>.</summary>
    public int? EntityId { get; set; }

    public DateTime OccurredOn { get; set; }
    public string Details { get; set; } = string.Empty;
    public int? ActorUserId { get; set; }

    /// <summary>Legacy alias of <see cref="ActorUserId"/>.</summary>
    public int? UserId { get; set; }

    public string? ActorRole { get; set; }
    public string Outcome { get; set; } = "Success";
    public string? ClientIp { get; set; }
    public string? CorrelationId { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
}
