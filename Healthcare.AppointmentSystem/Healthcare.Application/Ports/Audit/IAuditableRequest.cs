namespace Healthcare.Application.Ports.Audit;

/// <summary>
/// Marks a MediatR request for automatic immutable audit logging via
/// <see cref="Behaviors.AuditLoggingBehavior{TRequest,TResponse}"/>.
/// </summary>
public interface IAuditableRequest
{
    /// <summary>Canonical action name (see Domain.Audit.AuditActions).</summary>
    string AuditAction { get; }

    /// <summary>Resource type (Patient, Appointment, Payment, …).</summary>
    string AuditResourceType { get; }

    /// <summary>
    /// Resource id if known before the handler runs; otherwise null and
    /// <see cref="ResolveResourceId"/> is used after the handler.
    /// </summary>
    int? AuditResourceId { get; }

    /// <summary>Sanitized details object (serialized to JSON). Avoid PHI free text.</summary>
    object GetAuditDetails();

    /// <summary>Extract resource id from a successful response when not known up front.</summary>
    int? ResolveResourceId(object? response) => AuditResourceId;
}
