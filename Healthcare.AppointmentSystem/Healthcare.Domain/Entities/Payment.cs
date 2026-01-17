using Healthcare.Domain.Common;
using Healthcare.Domain.Enums;
using Healthcare.Domain.Events;
using Healthcare.Domain.ValueObjects;

namespace Healthcare.Domain.Entities;

/// <summary>
/// Represents a payment transaction for an appointment.
/// </summary>
/// <remarks>
/// Design Pattern: Aggregate Root + State Machine
/// 
/// State Transitions:
/// Pending → Succeeded, Failed
/// Succeeded → Refunded (if cancellation within allowed period)
/// Failed → (terminal state)
/// Refunded → (terminal state)
/// 
/// Business Rules:
/// 1. Cannot refund a failed payment
/// 2. Cannot refund twice
/// 3. Payment amount must match appointment consultation fee
/// </remarks>
public sealed class Payment : Entity
{
    /// <summary>
    /// Gets the appointment ID this payment is for.
    /// </summary>
    public int AppointmentId { get; private set; }

    /// <summary>
    /// Gets the navigation property to the appointment.
    /// </summary>
    public Appointment? Appointment { get; private set; }

    /// <summary>
    /// Gets the amount paid.
    /// </summary>
    public Money Amount { get; private set; } = null!;

    /// <summary>
    /// Gets the payment status.
    /// </summary>
    public PaymentStatus Status { get; private set; }

    /// <summary>
    /// Gets the transaction ID from the payment gateway.
    /// </summary>
    public TransactionId? TransactionId { get; private set; }

    /// <summary>
    /// Gets the payment method (e.g., "card", "paypal").
    /// </summary>
    public string PaymentMethod { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the payment processor name (e.g., "Stripe", "PayPal").
    /// </summary>
    public string PaymentProcessor { get; private set; } = string.Empty;

    /// <summary>
    /// Gets when the payment was completed.
    /// </summary>
    public DateTime? PaidAt { get; private set; }

    /// <summary>
    /// Gets when the payment was refunded.
    /// </summary>
    public DateTime? RefundedAt { get; private set; }

    /// <summary>
    /// Gets the refund transaction ID (if refunded).
    /// </summary>
    public TransactionId? RefundTransactionId { get; private set; }

    /// <summary>
    /// Gets the failure reason (if payment failed).
    /// </summary>
    public string? FailureReason { get; private set; }

    // Private constructor for EF Core
    private Payment() { }

    private Payment(
        int appointmentId,
        Money amount,
        string paymentProcessor)
    {
        AppointmentId = appointmentId;
        Amount = amount;
        PaymentProcessor = paymentProcessor;
        Status = PaymentStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a new pending payment.
    /// </summary>
    public static Payment Create(
        int appointmentId,
        Money amount,
        string paymentProcessor = "Stripe")
    {
        Guard.AgainstNull(amount, nameof(amount));
        Guard.AgainstNullOrWhiteSpace(paymentProcessor, nameof(paymentProcessor));

        if (appointmentId <= 0)
        {
            throw new ArgumentException("Appointment ID must be positive.", nameof(appointmentId));
        }

        return new Payment(appointmentId, amount, paymentProcessor.Trim());
    }

    /// <summary>
    /// Marks the payment as succeeded.
    /// </summary>
    public void MarkAsSucceeded(TransactionId transactionId, string paymentMethod)
    {
        Guard.AgainstNull(transactionId, nameof(transactionId));
        Guard.AgainstNullOrWhiteSpace(paymentMethod, nameof(paymentMethod));

        if (Status != PaymentStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Cannot mark payment as succeeded. Current status: {Status}");
        }

        Status = PaymentStatus.Succeeded;
        TransactionId = transactionId;
        PaymentMethod = paymentMethod.Trim();
        PaidAt = DateTime.UtcNow;
        MarkAsModified();

        AddDomainEvent(new PaymentSucceededEvent(Id, AppointmentId, Amount, transactionId));
    }

    /// <summary>
    /// Marks the payment as failed.
    /// </summary>
    public void MarkAsFailed(string failureReason)
    {
        Guard.AgainstNullOrWhiteSpace(failureReason, nameof(failureReason));

        if (Status != PaymentStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Cannot mark payment as failed. Current status: {Status}");
        }

        Status = PaymentStatus.Failed;
        FailureReason = failureReason.Trim();
        MarkAsModified();

        AddDomainEvent(new PaymentFailedEvent(Id, AppointmentId, failureReason));
    }

    /// <summary>
    /// Initiates a refund for this payment.
    /// </summary>
    public void InitiateRefund()
    {
        if (Status != PaymentStatus.Succeeded)
        {
            throw new InvalidOperationException(
                "Can only refund succeeded payments.");
        }

        if (Status == PaymentStatus.Refunded || Status == PaymentStatus.RefundPending)
        {
            throw new InvalidOperationException("Payment is already refunded or being refunded.");
        }

        Status = PaymentStatus.RefundPending;
        MarkAsModified();
    }

    /// <summary>
    /// Marks the refund as completed.
    /// </summary>
    public void CompleteRefund(TransactionId refundTransactionId)
    {
        Guard.AgainstNull(refundTransactionId, nameof(refundTransactionId));

        if (Status != PaymentStatus.RefundPending)
        {
            throw new InvalidOperationException(
                $"Cannot complete refund. Current status: {Status}");
        }

        Status = PaymentStatus.Refunded;
        RefundTransactionId = refundTransactionId;
        RefundedAt = DateTime.UtcNow;
        MarkAsModified();

        AddDomainEvent(new PaymentRefundedEvent(Id, AppointmentId, Amount, refundTransactionId));
    }

    /// <summary>
    /// Checks if the payment can be refunded.
    /// </summary>
    public bool CanBeRefunded() => Status == PaymentStatus.Succeeded;

    /// <summary>
    /// Checks if the payment is in a terminal state.
    /// </summary>
    public bool IsTerminal() =>
        Status == PaymentStatus.Succeeded ||
        Status == PaymentStatus.Failed ||
        Status == PaymentStatus.Refunded;
}
}