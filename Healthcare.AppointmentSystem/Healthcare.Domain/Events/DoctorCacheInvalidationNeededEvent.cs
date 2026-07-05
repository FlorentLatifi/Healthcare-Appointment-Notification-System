using Healthcare.Domain.Common;

namespace Healthcare.Domain.Events;

public sealed class DoctorCacheInvalidationNeededEvent : IDomainEvent
{
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }

    public DoctorCacheInvalidationNeededEvent()
    {
        EventId = Guid.NewGuid();
        OccurredOn = DateTime.UtcNow;
    }
}
