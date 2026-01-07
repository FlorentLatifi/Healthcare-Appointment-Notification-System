using FluentValidation;
using Healthcare.Presentation.API.Requests;

namespace Healthcare.Presentation.API.Validators;

/// <summary>
/// Validator for BookAppointmentRequest.
/// </summary>
/// <remarks>
/// Design Pattern: Strategy Pattern (FluentValidation)
/// 
/// Validation Rules:
/// - PatientId must be positive
/// - DoctorId must be positive
/// - ScheduledTime must be in the future
/// - Reason must be at least 10 characters
/// </remarks>
public sealed class BookAppointmentRequestValidator : AbstractValidator<BookAppointmentRequest>
{
    public BookAppointmentRequestValidator()
    {
        RuleFor(x => x.PatientId)
            .GreaterThan(0)
            .WithMessage("Patient ID must be greater than 0");

        RuleFor(x => x.DoctorId)
            .GreaterThan(0)
            .WithMessage("Doctor ID must be greater than 0");

        RuleFor(x => x.ScheduledTime)
            .GreaterThan(DateTime.UtcNow)
            .WithMessage("Appointment time must be in the future");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Reason is required")
            .MinimumLength(10)
            .WithMessage("Reason must be at least 10 characters")
            .MaximumLength(500)
            .WithMessage("Reason cannot exceed 500 characters");
    }
}