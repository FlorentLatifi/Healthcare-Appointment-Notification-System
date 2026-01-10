using FluentAssertions;
using Healthcare.Domain.Common;
using Healthcare.Domain.ValueObjects;
using Xunit;

namespace Healthcare.UnitTests.Domain.ValueObjects;

/// <summary>
/// Unit tests for Address value object.
/// </summary>
public class AddressTests
{
    #region Creation Tests

    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        // Arrange
        const string street = "123 Main Street";
        const string city = "Pristina";
        const string state = "Kosovo";
        const string postalCode = "10000";
        const string country = "Kosovo";

        // Act
        var address = Address.Create(street, city, state, postalCode, country);

        // Assert
        address.Should().NotBeNull();
        address.Street.Should().Be(street);
        address.City.Should().Be(city);
        address.State.Should().Be(state);
        address.PostalCode.Should().Be(postalCode.ToUpperInvariant());
        address.Country.Should().Be(country);
    }

    [Fact]
    public void Create_ShouldTrimWhitespace()
    {
        // Act
        var address = Address.Create(
            "  123 Main St  ",
            "  Pristina  ",
            "  Kosovo  ",
            "  10000  ",
            "  Kosovo  ");

        // Assert
        address.Street.Should().Be("123 Main St");
        address.City.Should().Be("Pristina");
        address.State.Should().Be("Kosovo");
        address.PostalCode.Should().Be("10000");
        address.Country.Should().Be("Kosovo");
    }

    [Fact]
    public void Create_ShouldNormalizePostalCodeToUpperCase()
    {
        // Act
        var address = Address.Create(
            "123 Main St",
            "New York",
            "NY",
            "ny-10001",
            "USA");

        // Assert
        address.PostalCode.Should().Be("NY-10001");
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData("", "City", "State", "10000", "Country")]
    [InlineData("   ", "City", "State", "10000", "Country")]
    public void Create_WithNullOrWhitespaceStreet_ShouldThrowArgumentException(
        string street, string city, string state, string postalCode, string country)
    {
        // Act
        Action act = () => Address.Create(street, city, state, postalCode, country);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*street*");
    }

    [Theory]
    [InlineData("Street", "", "State", "10000", "Country")]
    [InlineData("Street", "   ", "State", "10000", "Country")]
    public void Create_WithNullOrWhitespaceCity_ShouldThrowArgumentException(
        string street, string city, string state, string postalCode, string country)
    {
        // Act
        Action act = () => Address.Create(street, city, state, postalCode, country);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*city*");
    }

    [Theory]
    [InlineData("Street", "City", "", "10000", "Country")]
    [InlineData("Street", "City", "   ", "10000", "Country")]
    public void Create_WithNullOrWhitespaceState_ShouldThrowArgumentException(
        string street, string city, string state, string postalCode, string country)
    {
        // Act
        Action act = () => Address.Create(street, city, state, postalCode, country);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*state*");
    }

    [Theory]
    [InlineData("Street", "City", "State", "", "Country")]
    [InlineData("Street", "City", "State", "   ", "Country")]
    public void Create_WithNullOrWhitespacePostalCode_ShouldThrowArgumentException(
        string street, string city, string state, string postalCode, string country)
    {
        // Act
        Action act = () => Address.Create(street, city, state, postalCode, country);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*postalCode*");
    }

    [Theory]
    [InlineData("Street", "City", "State", "10000", "")]
    [InlineData("Street", "City", "State", "10000", "   ")]
    public void Create_WithNullOrWhitespaceCountry_ShouldThrowArgumentException(
        string street, string city, string state, string postalCode, string country)
    {
        // Act
        Action act = () => Address.Create(street, city, state, postalCode, country);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*country*");
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameValues_ShouldReturnTrue()
    {
        // Arrange
        var address1 = Address.Create("123 Main St", "Pristina", "Kosovo", "10000", "Kosovo");
        var address2 = Address.Create("123 Main St", "Pristina", "Kosovo", "10000", "Kosovo");

        // Act & Assert
        address1.Should().Be(address2);
        (address1 == address2).Should().BeTrue();
        address1.GetHashCode().Should().Be(address2.GetHashCode());
    }

    [Fact]
    public void Equals_IsCaseInsensitive()
    {
        // Arrange
        var address1 = Address.Create("123 MAIN ST", "PRISTINA", "KOSOVO", "10000", "KOSOVO");
        var address2 = Address.Create("123 main st", "pristina", "kosovo", "10000", "kosovo");

        // Act & Assert
        address1.Should().Be(address2);
    }

    [Fact]
    public void Equals_WithDifferentStreet_ShouldReturnFalse()
    {
        // Arrange
        var address1 = Address.Create("123 Main St", "Pristina", "Kosovo", "10000", "Kosovo");
        var address2 = Address.Create("456 Oak Ave", "Pristina", "Kosovo", "10000", "Kosovo");

        // Act & Assert
        address1.Should().NotBe(address2);
        (address1 != address2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentCity_ShouldReturnFalse()
    {
        // Arrange
        var address1 = Address.Create("123 Main St", "Pristina", "Kosovo", "10000", "Kosovo");
        var address2 = Address.Create("123 Main St", "Prishtina", "Kosovo", "10000", "Kosovo");

        // Act & Assert
        address1.Should().NotBe(address2);
    }

    #endregion

    #region Display Tests

    [Fact]
    public void GetFullAddress_ShouldReturnFormattedAddress()
    {
        // Arrange
        var address = Address.Create(
            "123 Main Street",
            "Pristina",
            "Kosovo",
            "10000",
            "Kosovo");

        // Act
        var result = address.GetFullAddress();

        // Assert
        result.Should().Be("123 Main Street, Pristina, Kosovo 10000, Kosovo");
    }

    [Fact]
    public void ToString_ShouldReturnFormattedAddress()
    {
        // Arrange
        var address = Address.Create(
            "456 Oak Avenue",
            "New York",
            "NY",
            "10001",
            "USA");

        // Act
        var result = address.ToString();

        // Assert
        result.Should().Be("456 Oak Avenue, New York, NY 10001, USA");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Create_WithSpecialCharacters_ShouldSucceed()
    {
        // Act
        var address = Address.Create(
            "123 O'Brien St. #5",
            "São Paulo",
            "São Paulo",
            "01000-000",
            "Brasil");

        // Assert
        address.Should().NotBeNull();
        address.Street.Should().Be("123 O'Brien St. #5");
        address.City.Should().Be("São Paulo");
    }

    [Fact]
    public void Create_WithVeryLongStreet_ShouldSucceed()
    {
        // Arrange
        var longStreet = "1234567890 Very Long Street Name With Multiple Words And Numbers";

        // Act
        var address = Address.Create(
            longStreet,
            "City",
            "State",
            "12345",
            "Country");

        // Assert
        address.Street.Should().Be(longStreet);
    }

    #endregion
}