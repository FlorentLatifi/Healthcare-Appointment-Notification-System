namespace Healthcare.Domain.Enums;

/// <summary>
/// Outcome of an audited operation (HIPAA/GDPR accountability).
/// </summary>
public enum AuditOutcome
{
    Success = 0,
    Failure = 1
}
