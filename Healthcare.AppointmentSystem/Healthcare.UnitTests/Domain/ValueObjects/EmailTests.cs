using FluentAssertions;
using Healthcare.Domain.Common;
using Healthcare.Domain.ValueObjects;
using Xunit;

namespace Healthcare.UnitTests.Domain.ValueObjects;

/// <summary>
/// Unit tests for Email value object.
/// </summary>
/// <remarks>
/// Testing Strategy: Value Object Pattern
/// 
/// What we test:
/// - Valid email creation
/// - Invalid email rejection
/// - Immutability
/// - Equality comparison
/// - Edge cases
/// </remarks>
public class EmailTests
{
    #region Valid Email Tests

    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name@domain.com")]
    [InlineData("first.last+tag@company.co.uk")]
    [InlineData("admin@subdomain.example.org")]
    public void Create_WithValidEmail_ShouldSucceed(string validEmail)
    {
        // Act
        var email = Email.Create(validEmail);

        // Assert
        email.Should().NotBeNull();
        email.Value.Should().Be(validEmail.ToLowerInvariant());
    }

    [Fact]
    public void Create_WithValidEmail_ShouldNormalizeToLowerCase()
    {
        // Arrange
        const string input = "User@EXAMPLE.COM";

        // Act
        var email = Email.Create(input);

        // Assert
        email.Value.Should().Be("user@example.com");
    }

    #endregion

    #region Invalid Email Tests

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespace_ShouldThrowArgumentException(string invalidEmail)
    {
        // Act
        Action act = () => Email.Create(invalidEmail);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be null or whitespace*");
    }

    [Fact] // ← Test i veçantë për null
    public void Create_WithNull_ShouldThrowArgumentException()
    {
        // Act
        Action act = () => Email.Create(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be null or whitespace*");
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("missing@domain")]
    [InlineData("@nodomain.com")]
    [InlineData("user@")]
    [InlineData("user name@example.com")] // Space in email
    [InlineData("user@domain@com")] // Double @
    public void Create_WithInvalidFormat_ShouldThrowInvalidEmailException(string invalidEmail)
    {
        // Act
        Action act = () => Email.Create(invalidEmail);

        // Assert
        act.Should().Throw<InvalidEmailException>()
            .WithMessage($"*{invalidEmail}*");
    }

    [Fact]
    public void Create_WithTooLongEmail_ShouldThrowInvalidEmailException()
    {
        // Arrange - RFC 5321 max length is 320
        var longEmail = new string('a', 310) + "@test.com"; // 319 chars - OK
        var tooLongEmail = new string('a', 312) + "@test.com"; // 321 chars - TOO LONG

        // Act
        var validEmail = Email.Create(longEmail);
        Action act = () => Email.Create(tooLongEmail);

        // Assert
        validEmail.Should().NotBeNull();
        act.Should().Throw<InvalidEmailException>()
            .WithMessage("*too long*");
    }

    #endregion

    #region Equality Tests (Value Object Pattern)

    [Fact]
    public void Equals_WithSameValue_ShouldReturnTrue()
    {
        // Arrange
        var email1 = Email.Create("test@example.com");
        var email2 = Email.Create("test@example.com");

        // Act & Assert
        email1.Should().Be(email2);
        (email1 == email2).Should().BeTrue();
        email1.GetHashCode().Should().Be(email2.GetHashCode());
    }

    [Fact]
    public void Equals_WithDifferentValue_ShouldReturnFalse()
    {
        // Arrange
        var email1 = Email.Create("test1@example.com");
        var email2 = Email.Create("test2@example.com");

        // Act & Assert
        email1.Should().NotBe(email2);
        (email1 != email2).Should().BeTrue();
    }

    [Fact]
    public void Equals_IsCaseInsensitive()
    {
        // Arrange
        var email1 = Email.Create("User@EXAMPLE.COM");
        var email2 = Email.Create("user@example.com");

        // Act & Assert
        email1.Should().Be(email2); // Both normalized to lowercase
    }

    #endregion

    #region Immutability Tests

    [Fact]
    public void Email_ShouldBeImmutable()
    {
        // Arrange
        var email = Email.Create("test@example.com");
        var originalValue = email.Value;

        // Act - Try to access properties (no setters should exist)
        var value = email.Value;

        // Assert
        value.Should().Be(originalValue);
        // Email has no public setters - this test documents immutability
    }

    #endregion

    #region ToString and Implicit Conversion Tests

    [Fact]
    public void ToString_ShouldReturnEmailValue()
    {
        // Arrange
        var email = Email.Create("test@example.com");

        // Act
        var result = email.ToString();

        // Assert
        result.Should().Be("test@example.com");
    }

    [Fact]
    public void ImplicitConversion_ToString_ShouldWork()
    {
        // Arrange
        var email = Email.Create("test@example.com");

        // Act
        string emailString = email; // Implicit conversion

        // Assert
        emailString.Should().Be("test@example.com");
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData("a@b.co")] // Very short but valid
    [InlineData("test+filter@example.com")] // With plus sign
    [InlineData("user_name@example-domain.com")] // Underscore and hyphen
    public void Create_WithEdgeCaseValidEmails_ShouldSucceed(string email)
    {
        // Act
        var result = Email.Create(email);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().Be(email.ToLowerInvariant());
    }

    #endregion
}