using System.Collections.Concurrent;
using Healthcare.Application.Common;
using Healthcare.Application.Ports.Payments;

namespace Healthcare.IntegrationTests.Helpers;

/// <summary>
/// In-memory stand-in for Stripe, so payment flows run without network access or real keys.
/// It behaves like a PaymentIntent that the client has already paid: confirming returns the
/// amount, currency and metadata recorded at creation, so the API's own binding checks
/// (appointment id, amount, currency) still run against real values.
/// </summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    private sealed record Intent(long AmountInCents, string Currency, IReadOnlyDictionary<string, string> Metadata);

    private readonly ConcurrentDictionary<string, Intent> _intents = new();

    public Task<Result<PaymentIntentResult>> CreatePaymentIntentAsync(
        decimal amount,
        string currency,
        string description,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        // Same shape as a real Stripe id: "pi_" followed by letters and digits only.
        var id = $"pi_Test{Guid.NewGuid():N}";
        // Test fees are in USD, which Stripe counts in cents.
        var cents = (long)Math.Round(amount * 100m);
        _intents[id] = new Intent(cents, currency.ToLowerInvariant(), metadata ?? new Dictionary<string, string>());

        return Task.FromResult(Result<PaymentIntentResult>.Success(new PaymentIntentResult
        {
            PaymentIntentId = id,
            ClientSecret = $"{id}_secret_test",
            Status = "requires_payment_method",
            AmountInCents = cents,
        }));
    }

    public Task<Result<PaymentConfirmationResult>> ConfirmPaymentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        if (!_intents.TryGetValue(paymentIntentId, out var intent))
            return Task.FromResult(Result<PaymentConfirmationResult>.Failure(
                $"No such payment intent: '{paymentIntentId}'"));

        return Task.FromResult(Result<PaymentConfirmationResult>.Success(new PaymentConfirmationResult
        {
            Succeeded = true,
            PaymentMethod = "card",
            TransactionId = paymentIntentId,
            AmountInCents = intent.AmountInCents,
            Currency = intent.Currency,
            Metadata = intent.Metadata,
        }));
    }

    public Task<Result<PaymentStatusResult>> GetPaymentStatusAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        if (!_intents.TryGetValue(paymentIntentId, out var intent))
            return Task.FromResult(Result<PaymentStatusResult>.Failure(
                $"No such payment intent: '{paymentIntentId}'"));

        return Task.FromResult(Result<PaymentStatusResult>.Success(new PaymentStatusResult
        {
            Status = "succeeded",
            IsComplete = true,
            AmountInCents = intent.AmountInCents,
        }));
    }

    public Task<Result<RefundResult>> RefundPaymentAsync(
        string paymentIntentId,
        string currency,
        decimal? amount = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (!_intents.TryGetValue(paymentIntentId, out var intent))
            return Task.FromResult(Result<RefundResult>.Failure(
                $"No such payment intent: '{paymentIntentId}'"));

        return Task.FromResult(Result<RefundResult>.Success(new RefundResult
        {
            RefundId = $"re_test_{Guid.NewGuid():N}",
            Status = "succeeded",
            AmountRefundedInCents = amount.HasValue ? (long)Math.Round(amount.Value * 100m) : intent.AmountInCents,
        }));
    }
}
