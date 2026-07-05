using FluentAssertions;
using FluentValidation.Results;
using Healthcare.Presentation.API.Requests;
using Healthcare.Presentation.API.Validators;
using Xunit;

namespace Healthcare.UnitTests.Presentation.Validators;

public class BookAppointmentRequestValidatorTests
{
    private readonly BookAppointmentRequestValidator _validator = new();

    [Theory]
    [InlineData("Standard")]
    [InlineData("Insurance")]
    [InlineData("Emergency")]
    [InlineData("Vip")]
    public void AppointmentType_WithValidValue_ShouldPass(string type)
    {
        var result = _validator.Validate(new BookAppointmentRequest { AppointmentType = type });
        result.Errors.Should().NotContain(e => e.PropertyName == nameof(BookAppointmentRequest.AppointmentType));
    }

    [Theory]
    [InlineData("standard")]
    [InlineData("insurance")]
    [InlineData("emergency")]
    [InlineData("vip")]
    [InlineData("STANDARD")]
    [InlineData("INSURANCE")]
    [InlineData("EMERGENCY")]
    [InlineData("VIP")]
    public void AppointmentType_WithCaseInsensitiveVariant_ShouldPass(string type)
    {
        var result = _validator.Validate(new BookAppointmentRequest { AppointmentType = type });
        result.Errors.Should().NotContain(e => e.PropertyName == nameof(BookAppointmentRequest.AppointmentType));
    }

    [Theory]
    [InlineData("insurnace")]
    [InlineData("Gold")]
    [InlineData("Platinum")]
    [InlineData("Basic")]
    [InlineData("emergencyy")]
    [InlineData("vviip")]
    public void AppointmentType_WithInvalidValue_ShouldFail(string type)
    {
        var result = _validator.Validate(new BookAppointmentRequest { AppointmentType = type });
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(BookAppointmentRequest.AppointmentType) &&
            e.ErrorMessage == "Appointment type must be one of: Standard, Insurance, Emergency, Vip");
    }

    [Fact]
    public void AppointmentType_WithEmptyString_ShouldFail()
    {
        var result = _validator.Validate(new BookAppointmentRequest { AppointmentType = "" });
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(BookAppointmentRequest.AppointmentType) &&
            e.ErrorMessage == "Appointment type is required");
    }

    [Fact]
    public void AppointmentType_WithNull_ShouldFail()
    {
        var result = _validator.Validate(new BookAppointmentRequest { AppointmentType = null! });
        result.Errors.Should().Contain(e => e.PropertyName == nameof(BookAppointmentRequest.AppointmentType));
    }

    [Fact]
    public void FullRequest_WithValidData_ShouldPassAllRules()
    {
        var request = new BookAppointmentRequest
        {
            PatientId = 1,
            DoctorId = 2,
            ScheduledTime = DateTime.UtcNow.AddDays(7),
            Reason = "Annual checkup and consultation",
            AppointmentType = "Insurance"
        };

        var result = _validator.Validate(request);
        result.IsValid.Should().BeTrue();
    }
}
