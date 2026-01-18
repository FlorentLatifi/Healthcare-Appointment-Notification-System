using FluentValidation;
using Healthcare.Presentation.API.Requests;

namespace Healthcare.Presentation.API.Validators;

/// <summary>
/// Validator for CreatePaymentIntentRequest.
/// </summary>
public sealed class CreatePaymentIntentRequestValidator : AbstractValidator<CreatePaymentIntentRequest>
{
    public CreatePaymentIntentRequestValidator()
    {
        RuleFor(x => x.AppointmentId)
            .GreaterThan(0)
            .WithMessage("Appointment ID must be greater than 0");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Description))
            .WithMessage("Description cannot exceed 500 characters");
    }
}