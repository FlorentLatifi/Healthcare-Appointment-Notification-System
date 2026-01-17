namespace Healthcare.Domain.Enums;

/// <summary>
/// Represents the status of a payment transaction.
/// </summary>
public enum PaymentStatus
{
    /// <summary>
    /// Payment initiated but not yet processed.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Payment successfully completed.
    /// </summary>
    Succeeded = 2,

    /// <summary>
    /// Payment failed (insufficient funds, card declined, etc.).
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Payment was refunded to the customer.
    /// </summary>
    Refunded = 4,

    /// <summary>
    /// Payment refund is being processed.
    /// </summary>
    RefundPending = 5
}