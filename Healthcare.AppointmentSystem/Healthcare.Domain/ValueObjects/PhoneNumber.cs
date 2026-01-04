using Healthcare.Domain.Common;
using System.Text.RegularExpressions;

namespace Healthcare.Domain.ValueObjects;

/// <summary>
/// Represents a valid phone number as a value object.
/// </summary>
/// <remarks>
/// Design Pattern: Value Object Pattern
/// 
/// Supports international format: +[country code][number]
/// Example: +38349123456 (Kosovo), +1234567890 (US)
/// 
/// In a production system, you might use a library like libphonenumber
/// for more robust international phone validation.
/// </remarks>
public sealed class PhoneNumber : ValueObject
{
    // Simplified international format: +[1-3 digits country code][6-14 digits number]
    private static readonly Regex PhoneRegex = new(
        @"^\+\d{1,3}\d{6,14}$",
        RegexOptions.Compiled);

    /// <summary>
    /// Gets the phone number value in international format.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PhoneNumber"/> class.
    /// </summary>
    private PhoneNumber(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new PhoneNumber value object with validation.
    /// </summary>
    /// <param name="phoneNumber">The phone number string (must include country code with +).</param>
    /// <returns>A valid PhoneNumber value object.</returns>
    /// <exception cref="InvalidPhoneNumberException">Thrown when phone format is invalid.</exception>
    public static PhoneNumber Create(string phoneNumber)
    {
        Guard.AgainstNullOrWhiteSpace(phoneNumber, nameof(phoneNumber));

        // Remove all whitespace and common separators
        var normalized = Regex.Replace(phoneNumber, @"[\s\-\(\)]", "");

        if (!PhoneRegex.IsMatch(normalized))
        {
            throw new InvalidPhoneNumberException(phoneNumber);
        }

        return new PhoneNumber(normalized);
    }

    /// <summary>
    /// Gets the formatted phone number for display.
    /// Example: +383 49 123 456
    /// </summary>
    public string GetFormattedValue()
    {
        // Simple formatting - in production use proper phone formatting library
        if (Value.StartsWith("+383")) // Kosovo
        {
            return $"{Value[..4]} {Value[4..6]} {Value[6..9]} {Value[9..]}";
        }
        else if (Value.StartsWith("+1")) // US/Canada
        {
            return $"{Value[..2]} ({Value[2..5]}) {Value[5..8]}-{Value[8..]}";
        }

        // Default: +XXX XX XXX XXXX
        return Value.Length > 10
            ? $"{Value[..4]} {Value[4..6]} {Value[6..9]} {Value[9..]}"
            : Value;
    }

    /// <summary>
    /// Returns the phone number as a string.
    /// </summary>
    public override string ToString() => Value;

    /// <summary>
    /// Implicit conversion to string for convenience.
    /// </summary>
    public static implicit operator string(PhoneNumber phone) => phone.Value;

    /// <summary>
    /// Gets the equality components for value object comparison.
    /// </summary>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}