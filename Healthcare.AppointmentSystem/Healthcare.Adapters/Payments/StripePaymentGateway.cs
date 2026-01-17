using Healthcare.Application.Common;
using Healthcare.Application.Ports.Payments;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Healthcare.Adapters.Payments;

/// <summary>
/// Stripe implementation of IPaymentGateway.
/// </summary>
/// <remarks>
/// Design Pattern: Adapter Pattern
/// 
/// This ADAPTER wraps the Stripe SDK and implements our PORT (IPaymentGateway).
/// 
/// Benefits:
/// - Application layer doesn't know about Stripe
/// - Easy to swap with PayPal, Square, etc.
/// - Centralized error handling for Stripe exceptions
/// - Testable (can mock IPaymentGateway)
/// 
/// Stripe Flow:
/// 1. CreatePaymentIntent → Returns client secret for frontend
/// 2. Frontend collects payment details (Stripe.js)
/// 3. ConfirmPayment → Charges the customer
/// 4. RefundPayment → Returns money to customer
/// </remarks>
public sealed class StripePaymentGateway : IPaymentGateway
{
    private readonly StripeSettings _settings;
    private readonly ILogger<StripePaymentGateway> _logger;
    private readonly PaymentIntentService _paymentIntentService;
    private readonly RefundService _refundService;

    public StripePaymentGateway(
        StripeSettings settings,
        ILogger<StripePaymentGateway> logger)
    {
        _settings = settings;
        _logger = logger;

        // Configure Stripe API key
        StripeConfiguration.ApiKey = _settings.SecretKey;

        // Initialize Stripe services
        _paymentIntentService = new PaymentIntentService();
        _refundService = new RefundService();
    }

    public async Task<Result<PaymentIntentResult>> CreatePaymentIntentAsync(
        decimal amount,
        string currency,
        string description,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Creating Stripe payment intent: Amount={Amount} {Currency}, Description={Description}",
                amount, currency, description);

            // Stripe requires amounts in cents (smallest currency unit)
            var amountInCents = ConvertToSmallestUnit(amount, currency);

            var options = new PaymentIntentCreateOptions
            {
                Amount = amountInCents,
                Currency = currency.ToLowerInvariant(),
                Description = description,
                Metadata = metadata ?? new Dictionary<string, string>(),
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true // Allow card, Apple Pay, Google Pay, etc.
                }
            };

            var paymentIntent = await _paymentIntentService.CreateAsync(
                options,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Payment intent created successfully: ID={PaymentIntentId}, Status={Status}",
                paymentIntent.Id, paymentIntent.Status);

            return Result<PaymentIntentResult>.Success(new PaymentIntentResult
            {
                PaymentIntentId = paymentIntent.Id,
                ClientSecret = paymentIntent.ClientSecret,
                Status = paymentIntent.Status,
                AmountInCents = paymentIntent.Amount
            });
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error while creating payment intent");
            return Result<PaymentIntentResult>.Failure($"Payment gateway error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating payment intent");
            return Result<PaymentIntentResult>.Failure($"An unexpected error occurred: {ex.Message}");
        }
    }

    public async Task<Result<PaymentConfirmationResult>> ConfirmPaymentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Confirming payment intent: {PaymentIntentId}", paymentIntentId);

            // Retrieve payment intent to check status
            var paymentIntent = await _paymentIntentService.GetAsync(
                paymentIntentId,
                cancellationToken: cancellationToken);

            var succeeded = paymentIntent.Status == "succeeded";

            if (!succeeded && paymentIntent.Status != "requires_payment_method")
            {
                _logger.LogWarning(
                    "Payment intent {PaymentIntentId} is in unexpected status: {Status}",
                    paymentIntentId, paymentIntent.Status);
            }

            var result = new PaymentConfirmationResult
            {
                Succeeded = succeeded,
                TransactionId = paymentIntent.Id,
                PaymentMethod = paymentIntent.PaymentMethodTypes?.FirstOrDefault() ?? "unknown"
            };

            if (!succeeded)
            {
                result.FailureReason = paymentIntent.LastPaymentError?.Message
                    ?? paymentIntent.CancellationReason
                    ?? "Payment not completed";
            }

            _logger.LogInformation(
                "Payment confirmation result: ID={PaymentIntentId}, Succeeded={Succeeded}",
                paymentIntentId, succeeded);

            return Result<PaymentConfirmationResult>.Success(result);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error while confirming payment: {PaymentIntentId}", paymentIntentId);
            return Result<PaymentConfirmationResult>.Failure($"Payment gateway error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while confirming payment: {PaymentIntentId}", paymentIntentId);
            return Result<PaymentConfirmationResult>.Failure($"An unexpected error occurred: {ex.Message}");
        }
    }

    public async Task<Result<PaymentStatusResult>> GetPaymentStatusAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Retrieving payment status: {PaymentIntentId}", paymentIntentId);

            var paymentIntent = await _paymentIntentService.GetAsync(
                paymentIntentId,
                cancellationToken: cancellationToken);

            var result = new PaymentStatusResult
            {
                Status = paymentIntent.Status,
                IsComplete = paymentIntent.Status == "succeeded",
                AmountInCents = paymentIntent.Amount
            };

            return Result<PaymentStatusResult>.Success(result);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error while getting payment status: {PaymentIntentId}", paymentIntentId);
            return Result<PaymentStatusResult>.Failure($"Payment gateway error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while getting payment status: {PaymentIntentId}", paymentIntentId);
            return Result<PaymentStatusResult>.Failure($"An unexpected error occurred: {ex.Message}");
        }
    }

    public async Task<Result<RefundResult>> RefundPaymentAsync(
        string paymentIntentId,
        decimal? amount = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Processing refund: PaymentIntentId={PaymentIntentId}, Amount={Amount}, Reason={Reason}",
                paymentIntentId, amount, reason);

            var options = new RefundCreateOptions
            {
                PaymentIntent = paymentIntentId,
                Reason = reason switch
                {
                    _ when reason?.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true => "duplicate",
                    _ when reason?.Contains("fraud", StringComparison.OrdinalIgnoreCase) == true => "fraudulent",
                    _ => "requested_by_customer"
                }
            };

            // If partial refund, specify amount
            if (amount.HasValue)
            {
                options.Amount = ConvertToSmallestUnit(amount.Value, _settings.DefaultCurrency);
            }

            var refund = await _refundService.CreateAsync(options, cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Refund processed successfully: RefundId={RefundId}, Status={Status}, Amount={Amount}",
                refund.Id, refund.Status, refund.Amount);

            return Result<RefundResult>.Success(new RefundResult
            {
                RefundId = refund.Id,
                Status = refund.Status,
                AmountRefundedInCents = refund.Amount
            });
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error while processing refund: {PaymentIntentId}", paymentIntentId);
            return Result<RefundResult>.Failure($"Refund failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while processing refund: {PaymentIntentId}", paymentIntentId);
            return Result<RefundResult>.Failure($"An unexpected error occurred: {ex.Message}");
        }
    }

    /// <summary>
    /// Converts decimal amount to smallest currency unit (cents).
    /// </summary>
    /// <remarks>
    /// Stripe requires amounts in cents:
    /// - USD: $50.00 → 5000 cents
    /// - EUR: €50.00 → 5000 cents
    /// - JPY: ¥50 → 50 (yen has no decimal)
    /// </remarks>
    private static long ConvertToSmallestUnit(decimal amount, string currency)
    {
        // Zero-decimal currencies (JPY, KRW, etc.)
        var zeroDecimalCurrencies = new[] { "JPY", "KRW", "VND", "CLP" };

        if (zeroDecimalCurrencies.Contains(currency.ToUpperInvariant()))
        {
            return (long)amount;
        }

        // Standard currencies (multiply by 100 for cents)
        return (long)(amount * 100);
    }
}