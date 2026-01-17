using Healthcare.Application.Common;
using Healthcare.Domain.ValueObjects;

namespace Healthcare.Application.Ports.Payments;

/// <summary>
/// PORT for payment processing.
/// </summary>
/// <remarks>
/// Design Pattern: Adapter Pattern (Hexagonal Architecture)
/// 
/// This is a PORT (interface in Application layer).
/// The ADAPTER (Stripe implementation) lives in Infrastructure/Adapters layer.
/// 
/// Why this separation?
/// - Application layer defines WHAT it needs (process payment, refund)
/// - Adapters layer defines HOW to do it (Stripe API calls)
/// - Easy to swap Stripe with PayPal, Square, etc.
/// 
/// Benefits:
/// - Domain and Application are independent of payment provider
/// - Can mock this interface in tests
/// - Can switch payment providers without touching business logic
/// </remarks>
public interface IPaymentGateway
{
    /// <summary>
    /// Creates a payment intent (pending payment).
    /// </summary>
    /// <param name="amount">The amount to charge.</param>
    /// <param name="currency">The currency code (e.g., "USD").</param>
    /// <param name="description">Payment description.</param>
    /// <param name="metadata">Additional metadata (e.g., appointment ID).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with payment intent ID (client secret for frontend).</returns>
    Task<Result<PaymentIntentResult>> CreatePaymentIntentAsync(
        decimal amount,
        string currency,
        string description,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms a payment (after customer provides payment method).
    /// </summary>
    /// <param name="paymentIntentId">The payment intent ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with confirmation status.</returns>
    Task<Result<PaymentConfirmationResult>> ConfirmPaymentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the status of a payment.
    /// </summary>
    /// <param name="paymentIntentId">The payment intent ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with payment status.</returns>
    Task<Result<PaymentStatusResult>> GetPaymentStatusAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a refund for a payment.
    /// </summary>
    /// <param name="paymentIntentId">The original payment intent ID.</param>
    /// <param name="amount">Amount to refund (null = full refund).</param>
    /// <param name="reason">Reason for refund.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with refund transaction ID.</returns>
    Task<Result<RefundResult>> RefundPaymentAsync(
        string paymentIntentId,
        decimal? amount = null,
        string? reason = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of creating a payment intent.
/// </summary>
public sealed class PaymentIntentResult
{
    /// <summary>
    /// The payment intent ID (used to track the payment).
    /// </summary>
    public string PaymentIntentId { get; set; } = string.Empty;

    /// <summary>
    /// Client secret (sent to frontend for payment form).
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Payment status (usually "requires_payment_method").
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Amount in cents (e.g., 5000 = $50.00).
    /// </summary>
    public long AmountInCents { get; set; }
}

/// <summary>
/// Result of confirming a payment.
/// </summary>
public sealed class PaymentConfirmationResult
{
    /// <summary>
    /// Whether the payment succeeded.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Payment method used (e.g., "card", "paypal").
    /// </summary>
    public string PaymentMethod { get; set; } = string.Empty;

    /// <summary>
    /// Transaction ID from payment gateway.
    /// </summary>
    public string TransactionId { get; set; } = string.Empty;

    /// <summary>
    /// Failure reason (if payment failed).
    /// </summary>
    public string? FailureReason { get; set; }
}

/// <summary>
/// Result of checking payment status.
/// </summary>
public sealed class PaymentStatusResult
{
    /// <summary>
    /// Current payment status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Whether payment is complete.
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Amount in cents.
    /// </summary>
    public long AmountInCents { get; set; }
}

/// <summary>
/// Result of processing a refund.
/// </summary>
public sealed class RefundResult
{
    /// <summary>
    /// Refund transaction ID.
    /// </summary>
    public string RefundId { get; set; } = string.Empty;

    /// <summary>
    /// Refund status (usually "succeeded" or "pending").
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Amount refunded in cents.
    /// </summary>
    public long AmountRefundedInCents { get; set; }
}