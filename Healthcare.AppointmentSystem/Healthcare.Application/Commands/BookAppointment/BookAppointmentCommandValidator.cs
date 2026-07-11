using FluentValidation;

namespace Healthcare.Application.Commands.BookAppointment;

public sealed class BookAppointmentCommandValidator : AbstractValidator<BookAppointmentCommand>
{
    public BookAppointmentCommandValidator()
    {
        RuleFor(x => x.PatientId)
            .GreaterThan(0).WithMessage("PatientId must be a positive identifier.");

        RuleFor(x => x.DoctorId)
            .GreaterThan(0).WithMessage("DoctorId must be a positive identifier.");

        RuleFor(x => x.ScheduledTime)
            .NotEmpty().WithMessage("ScheduledTime is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MinimumLength(10).WithMessage("Reason must be at least 10 characters.")
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.");
    }
}
