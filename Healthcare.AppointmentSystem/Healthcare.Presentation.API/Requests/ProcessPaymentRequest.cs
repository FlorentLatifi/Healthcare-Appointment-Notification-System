namespace Healthcare.Presentation.API.Requests;

/// <summary>
/// Request model for processing a payment.
/// </summary>
/// <remarks>
/// This is called AFTER the customer provides payment details
/// via Stripe Elements on the frontend.
/// 
/// Flow:
/// 1. Frontend collects payment details (Stripe.js)
/// 2. Stripe confirms payment method
/// 3. Frontend calls this endpoint with payment_intent_id
/// 4. Backend confirms payment with Stripe
/// 5. Backend creates Payment entity and auto-confirms appointment
/// </remarks>
public sealed class ProcessPaymentRequest
{
    /// <summary>
    /// Gets or sets the appointment ID.
    /// </summary>
    /// <example>5</example>
    public int AppointmentId { get; set; }

    /// <summary>
    /// Gets or sets the payment intent ID from Stripe.
    /// </summary>
    /// <example>pi_3QK5ZB2eZvKYlo2C0X8Z5X6Y</example>
    public string PaymentIntentId { get; set; } = string.Empty;
}