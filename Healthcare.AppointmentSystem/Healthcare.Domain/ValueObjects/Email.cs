using Healthcare.Domain.Common;
using System.Text.RegularExpressions;

namespace Healthcare.Domain.ValueObjects;

/// <summary>
/// Represents a valid email address as a value object.
/// </summary>
/// <remarks>
/// Design Pattern: Value Object Pattern
/// 
/// Encapsulates email validation logic and ensures that invalid emails
/// cannot exist in the domain. This is "always valid" pattern - once
/// constructed, the object is guaranteed to be in a valid state.
/// 
/// Value objects are immutable and compared by value, not reference.
/// </remarks>
public sealed class Email : ValueObject
{
    // RFC 5322 compliant regex (simplified version)
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Gets the email address value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Email"/> class.
    /// </summary>
    /// <param name="value">The email address string.</param>
    /// <exception cref="InvalidEmailException">Thrown when email format is invalid.</exception>
    private Email(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new Email value object with validation.
    /// </summary>
    /// <param name="email">The email address string.</param>
    /// <returns>A valid Email value object.</returns>
    /// <exception cref="InvalidEmailException">Thrown when email format is invalid.</exception>
    public static Email Create(string email)
    {
        Guard.AgainstNullOrWhiteSpace(email, nameof(email));

        var normalizedEmail = email.Trim().ToLowerInvariant();

        if (!EmailRegex.IsMatch(normalizedEmail))
        {
            throw new InvalidEmailException(email);
        }

        if (normalizedEmail.Length > 320) // RFC 5321 max length
        {
            throw new InvalidEmailException($"{email} - Email too long (max 320 characters)");
        }

        return new Email(normalizedEmail);
    }

    /// <summary>
    /// Returns the email address as a string.
    /// </summary>
    public override string ToString() => Value;

    /// <summary>
    /// Implicit conversion to string for convenience.
    /// </summary>
    public static implicit operator string(Email email) => email.Value;

    /// <summary>
    /// Gets the equality components for value object comparison.
    /// </summary>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}