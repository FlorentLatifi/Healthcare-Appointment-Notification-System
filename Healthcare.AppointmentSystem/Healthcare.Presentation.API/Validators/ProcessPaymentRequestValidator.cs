using FluentValidation;
using Healthcare.Presentation.API.Requests;

namespace Healthcare.Presentation.API.Validators;

/// <summary>
/// Validator for ProcessPaymentRequest.
/// </summary>
public sealed class ProcessPaymentRequestValidator : AbstractValidator<ProcessPaymentRequest>
{
    public ProcessPaymentRequestValidator()
    {
        RuleFor(x => x.AppointmentId)
            .GreaterThan(0)
            .WithMessage("Appointment ID must be greater than 0");

        RuleFor(x => x.PaymentIntentId)
            .NotEmpty()
            .WithMessage("Payment Intent ID is required")
            .MinimumLength(10)
            .WithMessage("Payment Intent ID must be at least 10 characters")
            .Matches(@"^pi_[a-zA-Z0-9]+$")
            .WithMessage("Invalid Payment Intent ID format (must start with 'pi_')");
    }
}