using Healthcare.Application.Common;

namespace Healthcare.Application.Commands.ProcessPayment;

/// <summary>
/// Command to process a payment for an appointment.
/// </summary>
/// <remarks>
/// Design Pattern: Command Pattern + CQRS
/// 
/// This command represents the intention to charge a patient
/// for their appointment consultation fee.
/// 
/// Flow:
/// 1. Create payment intent (Stripe)
/// 2. Customer provides payment details (frontend)
/// 3. Confirm payment (this command)
/// 4. Update appointment status
/// </remarks>
public sealed class ProcessPaymentCommand : ICommand<Result<int>>
{
    /// <summary>
    /// Gets or sets the appointment ID to pay for.
    /// </summary>
    public int AppointmentId { get; set; }

    /// <summary>
    /// Gets or sets the payment intent ID (from Stripe).
    /// </summary>
    public string PaymentIntentId { get; set; } = string.Empty;
}