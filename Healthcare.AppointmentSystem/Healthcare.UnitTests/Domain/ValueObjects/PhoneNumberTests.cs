using FluentAssertions;
using Healthcare.Domain.Common;
using Healthcare.Domain.ValueObjects;
using Xunit;

namespace Healthcare.UnitTests.Domain.ValueObjects;

/// <summary>
/// Unit tests for PhoneNumber value object.
/// </summary>
public class PhoneNumberTests
{
    #region Valid PhoneNumber Tests

    [Theory]
    [InlineData("+38349123456")]      // Kosovo
    [InlineData("+1234567890")]       // US (simple)
    [InlineData("+447911123456")]     // UK
    [InlineData("+86123456789012")]   // China (long)
    [InlineData("+123456789")]        // Minimum length (1+3+6)
    public void Create_WithValidPhoneNumber_ShouldSucceed(string validPhone)
    {
        // Act
        var phone = PhoneNumber.Create(validPhone);

        // Assert
        phone.Should().NotBeNull();
        phone.Value.Should().Be(validPhone);
    }

    [Fact]
    public void Create_ShouldRemoveWhitespaceAndSeparators()
    {
        // Arrange
        const string inputWithSpaces = "+383 49 123 456";
        const string inputWithDashes = "+383-49-123-456";
        const string inputWithParentheses = "+1 (234) 567-8900";

        // Act
        var phone1 = PhoneNumber.Create(inputWithSpaces);
        var phone2 = PhoneNumber.Create(inputWithDashes);
        var phone3 = PhoneNumber.Create(inputWithParentheses);

        // Assert
        phone1.Value.Should().Be("+38349123456");
        phone2.Value.Should().Be("+38349123456");
        phone3.Value.Should().Be("+12345678900");
    }

    #endregion

    #region Invalid PhoneNumber Tests

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespace_ShouldThrowArgumentException(string invalidPhone)
    {
        // Act
        Action act = () => PhoneNumber.Create(invalidPhone);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be null or whitespace*");
    }

    [Fact]
    public void Create_WithNull_ShouldThrowArgumentException()
    {
        // Act
        Action act = () => PhoneNumber.Create(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("123456789")]         // Missing country code (+)
    [InlineData("+1")]                // Too short
    [InlineData("+12345")]            // Too short
    [InlineData("+abc123456789")]     // Contains letters
    [InlineData("383491234567")]      // Missing +
    public void Create_WithInvalidFormat_ShouldThrowInvalidPhoneNumberException(string invalidPhone)
    {
        // Act
        Action act = () => PhoneNumber.Create(invalidPhone);

        // Assert
        act.Should().Throw<InvalidPhoneNumberException>()
            .WithMessage($"*{invalidPhone}*");
    }

    // ✅ SHTO këtë test të ri për max length:
    [Fact]
    public void Create_WithTooLongPhoneNumber_ShouldThrowInvalidPhoneNumberException()
    {
        // Arrange - 18 digits total (3 country + 15 number = TOO LONG)
        var tooLongPhone = "+1234567890123456";

        // Act
        Action act = () => PhoneNumber.Create(tooLongPhone);

        // Assert
        act.Should().Throw<InvalidPhoneNumberException>()
            .WithMessage($"*{tooLongPhone}*");
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameValue_ShouldReturnTrue()
    {
        // Arrange
        var phone1 = PhoneNumber.Create("+38349123456");
        var phone2 = PhoneNumber.Create("+38349123456");

        // Act & Assert
        phone1.Should().Be(phone2);
        (phone1 == phone2).Should().BeTrue();
        phone1.GetHashCode().Should().Be(phone2.GetHashCode());
    }

    [Fact]
    public void Equals_WithDifferentValue_ShouldReturnFalse()
    {
        // Arrange
        var phone1 = PhoneNumber.Create("+38349123456");
        var phone2 = PhoneNumber.Create("+38349987654");

        // Act & Assert
        phone1.Should().NotBe(phone2);
        (phone1 != phone2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithSameNumberButDifferentFormatting_ShouldReturnTrue()
    {
        // Arrange - Same number, different formatting
        var phone1 = PhoneNumber.Create("+383 49 123 456");
        var phone2 = PhoneNumber.Create("+383-49-123-456");

        // Act & Assert
        phone1.Should().Be(phone2); // Both normalize to +38349123456
    }

    #endregion

    #region Formatting Tests

    [Theory]
    [InlineData("+38349123456", "+383 49 123 456")]      // Kosovo format
    [InlineData("+12345678901", "+1 (234) 567-8901")]    // US format
    public void GetFormattedValue_ShouldFormatCorrectly(string input, string expectedFormat)
    {
        // Arrange
        var phone = PhoneNumber.Create(input);

        // Act
        var formatted = phone.GetFormattedValue();

        // Assert
        formatted.Should().Be(expectedFormat);
    }

    [Fact]
    public void GetFormattedValue_WithUnknownCountryCode_ShouldUseDefaultFormat()
    {
        // Arrange - Country code not in specific formatting rules
        var phone = PhoneNumber.Create("+441234567890");

        // Act
        var formatted = phone.GetFormattedValue();

        // Assert
        formatted.Should().NotBeNullOrEmpty();
        formatted.Should().Contain("+44");
    }

    #endregion

    #region ToString and Implicit Conversion Tests

    [Fact]
    public void ToString_ShouldReturnPhoneValue()
    {
        // Arrange
        var phone = PhoneNumber.Create("+38349123456");

        // Act
        var result = phone.ToString();

        // Assert
        result.Should().Be("+38349123456");
    }

    [Fact]
    public void ImplicitConversion_ToString_ShouldWork()
    {
        // Arrange
        var phone = PhoneNumber.Create("+38349123456");

        // Act
        string phoneString = phone; // Implicit conversion

        // Assert
        phoneString.Should().Be("+38349123456");
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData("+123456789")]           // Minimum valid (1 country + 6 digits)
    [InlineData("+12345678901234567")]   // Maximum valid (3 country + 14 digits)
    public void Create_WithBoundaryLengths_ShouldSucceed(string phone)
    {
        // Act
        var result = PhoneNumber.Create(phone);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().Be(phone);
    }

    [Fact]
    public void Create_WithMixedSeparators_ShouldNormalize()
    {
        // Arrange
        const string messyPhone = "+383 (49)-123 456";

        // Act
        var phone = PhoneNumber.Create(messyPhone);

        // Assert
        phone.Value.Should().Be("+38349123456");
    }

    #endregion
}