using FluentAssertions;
using Healthcare.Application.Ports.Payments;
using Healthcare.Application.Services;
using Xunit;

namespace Healthcare.UnitTests.Application.Payments;

public sealed class PaymentIntentBindingTests
{
    [Fact]
    public void Validate_MatchingAppointmentId_Succeeds()
    {
        var confirmation = new PaymentConfirmationResult
        {
            Succeeded = true,
            AmountInCents = 5000,
            Currency = "eur",
            Metadata = new Dictionary<string, string> { ["appointment_id"] = "42" }
        };

        PaymentIntentBinding.Validate(confirmation, expectedAppointmentId: 42)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_MismatchedAppointmentId_Fails()
    {
        var confirmation = new PaymentConfirmationResult
        {
            Succeeded = true,
            Metadata = new Dictionary<string, string> { ["appointment_id"] = "1" }
        };

        var result = PaymentIntentBinding.Validate(confirmation, expectedAppointmentId: 2);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("bound to appointment 1");
        result.Error.Should().Contain("appointment 2");
    }

    [Fact]
    public void Validate_MissingMetadata_Fails()
    {
        var confirmation = new PaymentConfirmationResult
        {
            Succeeded = true,
            Metadata = new Dictionary<string, string>()
        };

        PaymentIntentBinding.Validate(confirmation, 1).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_AmountMismatch_Fails()
    {
        var confirmation = new PaymentConfirmationResult
        {
            Succeeded = true,
            AmountInCents = 9999,
            Currency = "eur",
            Metadata = new Dictionary<string, string> { ["appointment_id"] = "5" }
        };

        var result = PaymentIntentBinding.Validate(
            confirmation,
            expectedAppointmentId: 5,
            expectedAmount: 50m,
            expectedCurrency: "EUR");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("amount");
    }
}
