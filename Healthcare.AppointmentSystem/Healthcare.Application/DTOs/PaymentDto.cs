namespace Healthcare.Application.DTOs;

/// <summary>
/// Data Transfer Object for Payment entity.
/// </summary>
public sealed class PaymentDto
{
    /// <summary>
    /// Gets or sets the payment ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the appointment ID.
    /// </summary>
    public int AppointmentId { get; set; }

    /// <summary>
    /// Gets or sets the payment amount.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency code.
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the payment status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the transaction ID.
    /// </summary>
    public string? TransactionId { get; set; }

    /// <summary>
    /// Gets or sets the payment method.
    /// </summary>
    public string? PaymentMethod { get; set; }

    /// <summary>
    /// Gets or sets the payment processor name.
    /// </summary>
    public string PaymentProcessor { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the payment was made.
    /// </summary>
    public DateTime? PaidAt { get; set; }

    /// <summary>
    /// Gets or sets when the payment was refunded.
    /// </summary>
    public DateTime? RefundedAt { get; set; }

    /// <summary>
    /// Gets or sets the refund transaction ID.
    /// </summary>
    public string? RefundTransactionId { get; set; }

    /// <summary>
    /// Gets or sets the failure reason.
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Gets or sets when the payment was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}