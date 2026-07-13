namespace Healthcare.Application.Ports.Audit;

/// <summary>Default when no HTTP request is available (background jobs, unit tests).</summary>
public sealed class NullAuditContext : IAuditContext
{
    public static readonly NullAuditContext Instance = new();

    public int? ActorUserId => null;
    public string? ActorRole => null;
    public string? ClientIp => null;
    public string? UserAgent => null;
    public string? CorrelationId => Observability.CorrelationContext.Current;
}
