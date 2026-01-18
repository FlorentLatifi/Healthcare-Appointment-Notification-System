namespace Healthcare.Presentation.API.Requests;

/// <summary>
/// Request model for refunding a payment.
/// </summary>
public sealed class RefundPaymentRequest
{
    /// <summary>
    /// Gets or sets the payment ID to refund.
    /// </summary>
    /// <example>10</example>
    public int PaymentId { get; set; }

    /// <summary>
    /// Gets or sets the reason for refund.
    /// </summary>
    /// <example>Appointment cancelled by patient due to emergency</example>
    public string Reason { get; set; } = string.Empty;
}