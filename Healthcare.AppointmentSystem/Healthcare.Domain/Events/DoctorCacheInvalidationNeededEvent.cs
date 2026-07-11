using Healthcare.Domain.Common;

namespace Healthcare.Domain.Events;

public sealed class DoctorCacheInvalidationNeededEvent : IDomainEvent
{
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }

    /// <summary>When set, only that doctor's by-id/schedule keys are removed (lists always bump generation).</summary>
    public int? DoctorId { get; }

    public DoctorCacheInvalidationNeededEvent(int? doctorId = null)
    {
        EventId = Guid.NewGuid();
        OccurredOn = DateTime.UtcNow;
        DoctorId = doctorId;
    }
}
