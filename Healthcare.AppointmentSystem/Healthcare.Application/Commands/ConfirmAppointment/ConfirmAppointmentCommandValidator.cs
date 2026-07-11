using FluentValidation;

namespace Healthcare.Application.Commands.ConfirmAppointment;

public sealed class ConfirmAppointmentCommandValidator : AbstractValidator<ConfirmAppointmentCommand>
{
    public ConfirmAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId)
            .GreaterThan(0).WithMessage("AppointmentId must be a positive identifier.");

        When(x => x.OverridePaymentRequirement, () =>
        {
            RuleFor(x => x.OverrideReason)
                .NotEmpty().WithMessage("OverrideReason is required when overriding payment.")
                .MinimumLength(10).WithMessage("OverrideReason must be at least 10 characters.");
        });
    }
}
