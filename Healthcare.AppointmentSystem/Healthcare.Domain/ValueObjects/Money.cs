using Healthcare.Domain.Common;

namespace Healthcare.Domain.ValueObjects;

/// <summary>
/// Represents a monetary amount with currency as a value object.
/// </summary>
/// <remarks>
/// Design Pattern: Value Object Pattern
/// 
/// Money should never be represented as a simple decimal in domain logic.
/// This value object ensures:
/// 1. Amount and currency are always together
/// 2. Cannot compare or add amounts in different currencies
/// 3. Arithmetic operations maintain currency consistency
/// 
/// In production, consider using a library like NodaMoney for comprehensive
/// currency handling including exchange rates, rounding rules, etc.
/// </remarks>
public sealed class Money : ValueObject
{
    /// <summary>
    /// Gets the monetary amount.
    /// </summary>
    public decimal Amount { get; }

    /// <summary>
    /// Gets the ISO 4217 currency code (e.g., "USD", "EUR").
    /// </summary>
    public string Currency { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Money"/> class.
    /// </summary>
    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// Creates a new Money value object with validation.
    /// </summary>
    /// <param name="amount">The monetary amount.</param>
    /// <param name="currency">The ISO 4217 currency code.</param>
    /// <returns>A valid Money value object.</returns>
    /// <exception cref="InvalidMoneyException">Thrown when amount or currency is invalid.</exception>
    public static Money Create(decimal amount, string currency)
    {
        Guard.AgainstNullOrWhiteSpace(currency, nameof(currency));

        if (amount < 0)
        {
            throw new InvalidMoneyException("Amount cannot be negative.");
        }

        var normalizedCurrency = currency.Trim().ToUpperInvariant();

        if (normalizedCurrency.Length != 3)
        {
            throw new InvalidMoneyException(
                $"Currency code '{currency}' is invalid. Must be 3-letter ISO 4217 code.");
        }

        // Round to 2 decimal places for currency precision
        var roundedAmount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);

        return new Money(roundedAmount, normalizedCurrency);
    }

    /// <summary>
    /// Creates a Money object representing zero in the specified currency.
    /// </summary>
    public static Money Zero(string currency) => Create(0, currency);

    /// <summary>
    /// Adds two Money objects. Both must have the same currency.
    /// </summary>
    /// <exception cref="InvalidMoneyException">Thrown when currencies don't match.</exception>
    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidMoneyException(
                $"Cannot add amounts in different currencies: {left.Currency} and {right.Currency}");
        }

        return Create(left.Amount + right.Amount, left.Currency);
    }

    /// <summary>
    /// Subtracts two Money objects. Both must have the same currency.
    /// </summary>
    /// <exception cref="InvalidMoneyException">Thrown when currencies don't match or result is negative.</exception>
    public static Money operator -(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidMoneyException(
                $"Cannot subtract amounts in different currencies: {left.Currency} and {right.Currency}");
        }

        return Create(left.Amount - right.Amount, left.Currency);
    }

    /// <summary>
    /// Multiplies Money by a scalar value.
    /// </summary>
    public static Money operator *(Money money, decimal multiplier)
    {
        return Create(money.Amount * multiplier, money.Currency);
    }

    /// <summary>
    /// Compares two Money objects for greater than. Must have same currency.
    /// </summary>
    public static bool operator >(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidMoneyException(
                $"Cannot compare amounts in different currencies: {left.Currency} and {right.Currency}");
        }

        return left.Amount > right.Amount;
    }

    /// <summary>
    /// Compares two Money objects for less than. Must have same currency.
    /// </summary>
    public static bool operator <(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidMoneyException(
                $"Cannot compare amounts in different currencies: {left.Currency} and {right.Currency}");
        }

        return left.Amount < right.Amount;
    }

    /// <summary>
    /// Gets a formatted string representation for display.
    /// Example: "$125.50 USD" or "€99.99 EUR"
    /// </summary>
    public string ToDisplayString()
    {
        var symbol = Currency switch
        {
            "USD" => "$",
            "EUR" => "€",
            "GBP" => "£",
            _ => ""
        };

        return symbol != ""
            ? $"{symbol}{Amount:N2} {Currency}"
            : $"{Amount:N2} {Currency}";
    }

    /// <summary>
    /// Returns the money value as a string.
    /// </summary>
    public override string ToString() => ToDisplayString();

    /// <summary>
    /// Gets the equality components for value object comparison.
    /// </summary>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}