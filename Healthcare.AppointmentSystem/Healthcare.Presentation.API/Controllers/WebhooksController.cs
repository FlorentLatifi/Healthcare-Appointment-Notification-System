using System.Text;
using Asp.Versioning;
using Healthcare.Adapters.Payments;
using Healthcare.Application.Common;
using Healthcare.Application.Ports.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Stripe;

namespace Healthcare.Presentation.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/webhooks/stripe")]
[AllowAnonymous]
[DisableCors]
[DisableRateLimiting]
public sealed class WebhooksController : ControllerBase
{
    private readonly StripeSettings _stripeSettings;
    private readonly IPaymentReconciliationService _reconciliationService;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        StripeSettings stripeSettings,
        IPaymentReconciliationService reconciliationService,
        ILogger<WebhooksController> logger)
    {
        _stripeSettings = stripeSettings;
        _reconciliationService = reconciliationService;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> HandleStripeWebhook(CancellationToken cancellationToken)
    {
        Request.EnableBuffering();
        var json = await new StreamReader(Request.Body, Encoding.UTF8).ReadToEndAsync();
        Request.Body.Position = 0;

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                _stripeSettings.WebhookSecret,
                300,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            _logger.LogError("Stripe webhook signature verification failed: {Error}", ex.Message);
            return BadRequest("Webhook signature verification failed.");
        }

        _logger.LogInformation("Stripe webhook received: {Type}", stripeEvent.Type);

        if (stripeEvent.Data.Object is not PaymentIntent paymentIntent)
        {
            return Ok("Event data is not a PaymentIntent.");
        }

        if (!paymentIntent.Metadata.TryGetValue("appointment_id", out var appointmentIdStr)
            || !int.TryParse(appointmentIdStr, out var appointmentId))
        {
            _logger.LogWarning(
                "PaymentIntent {PaymentIntentId} is missing or has invalid 'appointment_id' metadata",
                paymentIntent.Id);
            return Ok("Missing or invalid appointment_id metadata.");
        }

        switch (stripeEvent.Type)
        {
            case "payment_intent.succeeded":
            {
                var transactionId = paymentIntent.LatestChargeId ?? paymentIntent.Id;
                var paymentMethod = paymentIntent.PaymentMethodTypes?.FirstOrDefault() ?? "card";

                var result = await _reconciliationService.ReconcilePaymentAsync(
                    appointmentId,
                    paymentIntent.Id,
                    succeeded: true,
                    transactionId,
                    paymentMethod,
                    failureReason: null,
                    cancellationToken);

                if (result.IsFailure)
                {
                    _logger.LogWarning(
                        "Payment reconciliation failed for PaymentIntent {PaymentIntentId}: {Error}",
                        paymentIntent.Id, result.Error);
                }

                return Ok(new { reconciled = result.IsSuccess, paymentId = result.IsSuccess ? result.Value : (int?)null });
            }

            case "payment_intent.payment_failed":
            {
                var failureReason = paymentIntent.LastPaymentError?.Message ?? "Unknown error";

                var result = await _reconciliationService.ReconcilePaymentAsync(
                    appointmentId,
                    paymentIntent.Id,
                    succeeded: false,
                    transactionId: paymentIntent.Id,
                    paymentMethod: "card",
                    failureReason,
                    cancellationToken);

                return Ok(new { reconciled = result.IsSuccess });
            }

            default:
                _logger.LogInformation("Unhandled Stripe webhook event type: {Type}", stripeEvent.Type);
                return Ok($"Unhandled event type: {stripeEvent.Type}");
        }
    }
}
