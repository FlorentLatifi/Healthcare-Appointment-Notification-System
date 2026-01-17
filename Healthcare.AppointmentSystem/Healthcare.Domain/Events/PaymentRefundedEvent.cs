using Healthcare.Domain.Common;
using Healthcare.Domain.ValueObjects;

namespace Healthcare.Domain.Events;

/// <summary>
/// Domain event raised when a payment is refunded.
/// </summary>
public sealed class PaymentRefundedEvent : IDomainEvent
{
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }

    public int PaymentId { get; }
    public int AppointmentId { get; }
    public Money Amount { get; }
    public TransactionId RefundTransactionId { get; }

    public PaymentRefundedEvent(
        int paymentId,
        int appointmentId,
        Money amount,
        TransactionId refundTransactionId)
    {
        EventId = Guid.NewGuid();
        OccurredOn = DateTime.UtcNow;
        PaymentId = paymentId;
        AppointmentId = appointmentId;
        Amount = amount;
        RefundTransactionId = refundTransactionId;
    }
}