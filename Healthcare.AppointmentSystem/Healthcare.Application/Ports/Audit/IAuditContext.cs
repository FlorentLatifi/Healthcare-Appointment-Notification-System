namespace Healthcare.Application.Ports.Audit;

/// <summary>
/// Ambient request context for audit rows (actor, IP, correlation).
/// Implemented from HTTP in the Presentation layer; no-op outside a request.
/// </summary>
public interface IAuditContext
{
    int? ActorUserId { get; }
    string? ActorRole { get; }
    string? ClientIp { get; }
    string? UserAgent { get; }
    string? CorrelationId { get; }
}
