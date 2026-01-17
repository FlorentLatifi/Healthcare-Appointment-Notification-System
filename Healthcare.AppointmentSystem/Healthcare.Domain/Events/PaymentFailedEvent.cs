using Healthcare.Domain.Common;

namespace Healthcare.Domain.Events;

/// <summary>
/// Domain event raised when a payment fails.
/// </summary>
public sealed class PaymentFailedEvent : IDomainEvent
{
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }

    public int PaymentId { get; }
    public int AppointmentId { get; }
    public string FailureReason { get; }

    public PaymentFailedEvent(
        int paymentId,
        int appointmentId,
        string failureReason)
    {
        EventId = Guid.NewGuid();
        OccurredOn = DateTime.UtcNow;
        PaymentId = paymentId;
        AppointmentId = appointmentId;
        FailureReason = failureReason;
    }
}