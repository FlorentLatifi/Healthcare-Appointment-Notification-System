using FluentAssertions;
using Healthcare.Domain.Common;
using Healthcare.Domain.ValueObjects;
using Xunit;


namespace Healthcare.UnitTests.Domain.ValueObjects;

/// <summary>
/// Unit tests for Money value object.
/// </summary>
/// <remarks>
/// Testing Strategy: Value Object Pattern with Business Rules
/// 
/// What we test:
/// - Valid money creation
/// - Currency validation
/// - Arithmetic operations (+, -, *)
/// - Currency mismatch protection
/// - Rounding behavior
/// - Comparison operators
/// </remarks>
public class MoneyTests
{
    #region Creation Tests

    [Theory]
    [InlineData(0, "USD")]
    [InlineData(10.50, "EUR")]
    [InlineData(999.99, "GBP")]
    [InlineData(1000000, "USD")]
    public void Create_WithValidAmountAndCurrency_ShouldSucceed(decimal amount, string currency)
    {
        // Act
        var money = Money.Create(amount, currency);

        // Assert
        money.Should().NotBeNull();
        money.Amount.Should().Be(amount);
        money.Currency.Should().Be(currency.ToUpperInvariant());
    }

    [Fact]
    public void Create_ShouldNormalizeCurrencyToUpperCase()
    {
        // Act
        var money = Money.Create(100, "usd");

        // Assert
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Create_ShouldTrimCurrency()
    {
        // Act
        var money = Money.Create(100, "  EUR  ");

        // Assert
        money.Currency.Should().Be("EUR");
    }

    [Fact]
    public void Create_ShouldRoundToTwoDecimalPlaces()
    {
        // Act
        var money = Money.Create(10.999m, "USD");

        // Assert
        money.Amount.Should().Be(11.00m); // Rounded up
    }

    [Fact]
    public void Zero_ShouldCreateMoneyWithZeroAmount()
    {
        // Act
        var money = Money.Zero("USD");

        // Assert
        money.Amount.Should().Be(0);
        money.Currency.Should().Be("USD");
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Create_WithNegativeAmount_ShouldThrowInvalidMoneyException()
    {
        // Act
        Action act = () => Money.Create(-10, "USD");

        // Assert
        act.Should().Throw<InvalidMoneyException>()
            .WithMessage("*cannot be negative*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespaceCurrency_ShouldThrowArgumentException(string currency)
    {
        // Act
        Action act = () => Money.Create(100, currency);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be null or whitespace*");
    }

    [Fact] // ← Test i veçantë për null
    public void Create_WithNullCurrency_ShouldThrowArgumentException()
    {
        // Act
        Action act = () => Money.Create(100, null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be null or whitespace*");
    }

    [Theory]
    [InlineData("US")] // Too short
    [InlineData("USDD")] // Too long
    [InlineData("U")] // Too short
    public void Create_WithInvalidCurrencyLength_ShouldThrowInvalidMoneyException(string currency)
    {
        // Act
        Action act = () => Money.Create(100, currency);

        // Assert
        act.Should().Throw<InvalidMoneyException>()
            .WithMessage("*3-letter ISO 4217*");
    }

    #endregion

    #region Arithmetic Operations Tests

    [Fact]
    public void Add_WithSameCurrency_ShouldSucceed()
    {
        // Arrange
        var money1 = Money.Create(10, "USD");
        var money2 = Money.Create(20, "USD");

        // Act
        var result = money1 + money2;

        // Assert
        result.Amount.Should().Be(30);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Add_WithDifferentCurrency_ShouldThrowInvalidMoneyException()
    {
        // Arrange
        var money1 = Money.Create(10, "USD");
        var money2 = Money.Create(20, "EUR");

        // Act
        Action act = () => { var result = money1 + money2; };

        // Assert
        act.Should().Throw<InvalidMoneyException>()
            .WithMessage("*different currencies*");
    }

    [Fact]
    public void Subtract_WithSameCurrency_ShouldSucceed()
    {
        // Arrange
        var money1 = Money.Create(50, "USD");
        var money2 = Money.Create(20, "USD");

        // Act
        var result = money1 - money2;

        // Assert
        result.Amount.Should().Be(30);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Subtract_WithDifferentCurrency_ShouldThrowInvalidMoneyException()
    {
        // Arrange
        var money1 = Money.Create(50, "USD");
        var money2 = Money.Create(20, "EUR");

        // Act
        Action act = () => { var result = money1 - money2; };

        // Assert
        act.Should().Throw<InvalidMoneyException>()
            .WithMessage("*different currencies*");
    }

    [Fact]
    public void Subtract_ResultingInNegative_ShouldThrowInvalidMoneyException()
    {
        // Arrange
        var money1 = Money.Create(10, "USD");
        var money2 = Money.Create(20, "USD");

        // Act
        Action act = () => { var result = money1 - money2; };

        // Assert
        act.Should().Throw<InvalidMoneyException>()
            .WithMessage("*cannot be negative*");
    }

    [Fact]
    public void Multiply_ByScalar_ShouldSucceed()
    {
        // Arrange
        var money = Money.Create(10, "USD");

        // Act
        var result = money * 3;

        // Assert
        result.Amount.Should().Be(30);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Multiply_ByDecimal_ShouldRoundResult()
    {
        // Arrange
        var money = Money.Create(10, "USD");

        // Act
        var result = money * 1.5m;

        // Assert
        result.Amount.Should().Be(15.00m);
    }

    #endregion

    #region Comparison Tests

    [Fact]
    public void GreaterThan_WithSameCurrency_ShouldCompareCorrectly()
    {
        // Arrange
        var money1 = Money.Create(50, "USD");
        var money2 = Money.Create(30, "USD");

        // Act & Assert
        (money1 > money2).Should().BeTrue();
        (money2 > money1).Should().BeFalse();
    }

    [Fact]
    public void LessThan_WithSameCurrency_ShouldCompareCorrectly()
    {
        // Arrange
        var money1 = Money.Create(30, "USD");
        var money2 = Money.Create(50, "USD");

        // Act & Assert
        (money1 < money2).Should().BeTrue();
        (money2 < money1).Should().BeFalse();
    }

    [Fact]
    public void GreaterThan_WithDifferentCurrency_ShouldThrowInvalidMoneyException()
    {
        // Arrange
        var money1 = Money.Create(50, "USD");
        var money2 = Money.Create(30, "EUR");

        // Act
        Action act = () => { var result = money1 > money2; };

        // Assert
        act.Should().Throw<InvalidMoneyException>()
            .WithMessage("*different currencies*");
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameAmountAndCurrency_ShouldReturnTrue()
    {
        // Arrange
        var money1 = Money.Create(100, "USD");
        var money2 = Money.Create(100, "USD");

        // Act & Assert
        money1.Should().Be(money2);
        (money1 == money2).Should().BeTrue();
        money1.GetHashCode().Should().Be(money2.GetHashCode());
    }

    [Fact]
    public void Equals_WithDifferentAmount_ShouldReturnFalse()
    {
        // Arrange
        var money1 = Money.Create(100, "USD");
        var money2 = Money.Create(200, "USD");

        // Act & Assert
        money1.Should().NotBe(money2);
        (money1 != money2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentCurrency_ShouldReturnFalse()
    {
        // Arrange
        var money1 = Money.Create(100, "USD");
        var money2 = Money.Create(100, "EUR");

        // Act & Assert
        money1.Should().NotBe(money2);
        (money1 != money2).Should().BeTrue();
    }

    #endregion

    #region Display Tests

    [Theory]
    [InlineData(100, "USD", "$100.00 USD")]
    [InlineData(99.99, "EUR", "€99.99 EUR")]
    [InlineData(50.50, "GBP", "£50.50 GBP")]
    [InlineData(75.25, "CHF", "75.25 CHF")] // No symbol
    public void ToDisplayString_ShouldFormatCorrectly(decimal amount, string currency, string expected)
    {
        // Arrange
        var money = Money.Create(amount, currency);

        // Act
        var result = money.ToDisplayString();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ToString_ShouldReturnDisplayString()
    {
        // Arrange
        var money = Money.Create(125.50m, "USD");

        // Act
        var result = money.ToString();

        // Assert
        result.Should().Be("$125.50 USD");
    }

    #endregion

    #region Rounding Tests

    [Theory]
    [InlineData(10.123, 10.12)]
    [InlineData(10.125, 10.13)] // Round away from zero
    [InlineData(10.999, 11.00)]
    [InlineData(10.001, 10.00)]
    public void Create_ShouldRoundToTwoDecimals(decimal input, decimal expected)
    {
        // Act
        var money = Money.Create(input, "USD");

        // Assert
        money.Amount.Should().Be(expected);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Create_WithZeroAmount_ShouldSucceed()
    {
        // Act
        var money = Money.Create(0, "USD");

        // Assert
        money.Amount.Should().Be(0);
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Create_WithVeryLargeAmount_ShouldSucceed()
    {
        // Act
        var money = Money.Create(999_999_999.99m, "USD");

        // Assert
        money.Amount.Should().Be(999_999_999.99m);
    }

    #endregion
}