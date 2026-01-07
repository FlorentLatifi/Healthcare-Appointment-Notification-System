using FluentValidation;
using Healthcare.Presentation.API.Requests;

namespace Healthcare.Presentation.API.Validators;

/// <summary>
/// Validator for CancelAppointmentRequest.
/// </summary>
public sealed class CancelAppointmentRequestValidator : AbstractValidator<CancelAppointmentRequest>
{
    public CancelAppointmentRequestValidator()
    {
        RuleFor(x => x.AppointmentId)
            .GreaterThan(0)
            .WithMessage("Appointment ID must be greater than 0");

        RuleFor(x => x.CancellationReason)
            .NotEmpty()
            .WithMessage("Cancellation reason is required")
            .MinimumLength(10)
            .WithMessage("Cancellation reason must be at least 10 characters")
            .MaximumLength(500)
            .WithMessage("Cancellation reason cannot exceed 500 characters");
    }
}