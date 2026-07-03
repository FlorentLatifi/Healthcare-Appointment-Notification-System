using FluentValidation;
using Healthcare.Presentation.API.Requests;

namespace Healthcare.Presentation.API.Validators;

/// <summary>
/// Validator for ConfirmAppointmentRequest.
/// </summary>
public sealed class ConfirmAppointmentRequestValidator : AbstractValidator<ConfirmAppointmentRequest>
{
    public ConfirmAppointmentRequestValidator()
    {
        RuleFor(x => x.AppointmentId)
            .GreaterThan(0)
            .WithMessage("Appointment ID must be greater than 0");

        RuleFor(x => x.OverrideReason)
            .NotEmpty()
            .WithMessage("Override reason is required when overriding the payment requirement")
            .MinimumLength(10)
            .WithMessage("Override reason must be at least 10 characters")
            .MaximumLength(500)
            .WithMessage("Override reason cannot exceed 500 characters")
            .When(x => x.OverridePaymentRequirement);
    }
}