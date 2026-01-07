using FluentValidation;
using Healthcare.Presentation.API.Requests;

namespace Healthcare.Presentation.API.Validators;

/// <summary>
/// Validator for CreateDoctorRequest.
/// </summary>
public sealed class CreateDoctorRequestValidator : AbstractValidator<CreateDoctorRequest>
{
    public CreateDoctorRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MinimumLength(2).WithMessage("First name must be at least 2 characters")
            .MaximumLength(50).WithMessage("First name cannot exceed 50 characters");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MinimumLength(2).WithMessage("Last name must be at least 2 characters")
            .MaximumLength(50).WithMessage("Last name cannot exceed 50 characters");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required")
            .Matches(@"^\+\d{1,3}\d{6,14}$")
            .WithMessage("Phone number must be in international format");

        RuleFor(x => x.LicenseNumber)
            .NotEmpty().WithMessage("License number is required")
            .MinimumLength(5).WithMessage("License number must be at least 5 characters")
            .MaximumLength(50).WithMessage("License number cannot exceed 50 characters");

        RuleFor(x => x.Specialty)
            .NotEmpty().WithMessage("Specialty is required");

        RuleFor(x => x.ConsultationFeeAmount)
            .GreaterThan(0).WithMessage("Consultation fee must be greater than 0")
            .LessThan(10000).WithMessage("Consultation fee seems unreasonably high");

        RuleFor(x => x.ConsultationFeeCurrency)
            .NotEmpty().WithMessage("Currency is required")
            .Length(3).WithMessage("Currency must be 3-letter code (e.g., USD, EUR)");

        RuleFor(x => x.YearsOfExperience)
            .GreaterThanOrEqualTo(0).WithMessage("Years of experience cannot be negative")
            .LessThanOrEqualTo(70).WithMessage("Years of experience seems too high");
    }
}