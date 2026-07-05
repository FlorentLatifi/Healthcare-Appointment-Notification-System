namespace Healthcare.Application.DTOs;

public sealed class AuditLogDto
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public DateTime OccurredOn { get; set; }
    public string Details { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
