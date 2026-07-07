using FluentAssertions;
using FluentValidation.Results;
using Healthcare.Presentation.API.Requests;
using Healthcare.Presentation.API.Validators;
using Xunit;

namespace Healthcare.UnitTests.Presentation.Validators;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    [Fact]
    public void Password_WhenWithinLimit_ShouldPass()
    {
        var result = _validator.Validate(new RegisterRequest { Password = "ValidP@ss1" });
        result.Errors.Should().NotContain(e => e.PropertyName == nameof(RegisterRequest.Password));
    }

    [Fact]
    public void Password_WhenExceedsMaximumLength_ShouldFail()
    {
        var longPassword = new string('A', 129) + "1a";
        var result = _validator.Validate(new RegisterRequest { Password = longPassword });
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(RegisterRequest.Password) &&
            e.ErrorMessage == "Password cannot exceed 128 characters");
    }
}
