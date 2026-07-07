using Healthcare.Domain.Common;

namespace Healthcare.Domain.Events;

/// <summary>
/// Domain event raised when a patient's record (appointments or profile)
/// is accessed by someone other than the patient themselves. Skipped for
/// self-access to avoid noisy audit logs — patients viewing their own data
/// is expected frequent behavior, not an auditable access.
/// </summary>
public sealed class PatientRecordAccessedEvent : IDomainEvent
{
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }

    public int PatientId { get; }

    /// <summary>
    /// The UserId of the person who accessed the record.
    /// </summary>
    public int? AccessedByUserId { get; }

    /// <summary>
    /// A human-readable description of the access (e.g., "Patient profile viewed").
    /// </summary>
    public string Description { get; }

    public PatientRecordAccessedEvent(
        int patientId,
        int? accessedByUserId,
        string description)
    {
        EventId = Guid.NewGuid();
        OccurredOn = DateTime.UtcNow;
        PatientId = patientId;
        AccessedByUserId = accessedByUserId;
        Description = description;
    }
}
