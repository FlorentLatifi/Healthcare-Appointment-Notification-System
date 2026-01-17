using Healthcare.Application.Common;

namespace Healthcare.Application.Commands.RefundPayment;

/// <summary>
/// Command to refund a payment.
/// </summary>
public sealed class RefundPaymentCommand : ICommand<Result>
{
    /// <summary>
    /// Gets or sets the payment ID to refund.
    /// </summary>
    public int PaymentId { get; set; }

    /// <summary>
    /// Gets or sets the refund reason.
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}