namespace Healthcare.Presentation.API.Requests;

/// <summary>
/// Request model for creating a payment intent.
/// </summary>
/// <remarks>
/// Design Pattern: Data Transfer Object (DTO)
/// 
/// This DTO:
/// - Receives data from HTTP POST request
/// - Validates input via FluentValidation
/// - Maps to payment gateway parameters
/// - Decouples API from Application layer
/// 
/// Flow:
/// 1. Frontend calls this endpoint before showing payment form
/// 2. Backend creates Stripe Payment Intent
/// 3. Returns client_secret to frontend
/// 4. Frontend uses Stripe.js to collect payment details
/// 5. Frontend calls ProcessPayment to complete
/// </remarks>
public sealed class CreatePaymentIntentRequest
{
    /// <summary>
    /// Gets or sets the appointment ID to create payment for.
    /// </summary>
    /// <example>5</example>
    public int AppointmentId { get; set; }

    /// <summary>
    /// Gets or sets optional description for the payment.
    /// </summary>
    /// <example>Consultation fee for Dr. Smith - Annual checkup</example>
    public string? Description { get; set; }
}