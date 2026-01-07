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
    }
}