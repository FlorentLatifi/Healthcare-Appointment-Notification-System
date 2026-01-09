using FluentValidation;
using Healthcare.Presentation.API.Requests;

namespace Healthcare.Presentation.API.Validators;

/// <summary>
/// Validator for LoginRequest.
/// </summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required");
    }
}