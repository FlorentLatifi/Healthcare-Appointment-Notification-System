using Healthcare.Domain.Common;

namespace Healthcare.Domain.ValueObjects;

/// <summary>
/// Represents a physical address as a value object.
/// </summary>
/// <remarks>
/// Design Pattern: Value Object Pattern
/// 
/// Addresses are immutable and compared by their complete address components.
/// This is simplified - production systems might integrate with address
/// validation services (Google Maps API, USPS, etc.)
/// </remarks>
public sealed class Address : ValueObject
{
    /// <summary>
    /// Gets the street address (line 1).
    /// </summary>
    public string Street { get; }

    /// <summary>
    /// Gets the city.
    /// </summary>
    public string City { get; }

    /// <summary>
    /// Gets the state or province.
    /// </summary>
    public string State { get; }

    /// <summary>
    /// Gets the postal or ZIP code.
    /// </summary>
    public string PostalCode { get; }

    /// <summary>
    /// Gets the country.
    /// </summary>
    public string Country { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Address"/> class.
    /// </summary>
    private Address(string street, string city, string state, string postalCode, string country)
    {
        Street = street;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
    }

    /// <summary>
    /// Creates a new Address value object with validation.
    /// </summary>
    public static Address Create(string street, string city, string state, string postalCode, string country)
    {
        Guard.AgainstNullOrWhiteSpace(street, nameof(street));
        Guard.AgainstNullOrWhiteSpace(city, nameof(city));
        Guard.AgainstNullOrWhiteSpace(state, nameof(state));
        Guard.AgainstNullOrWhiteSpace(postalCode, nameof(postalCode));
        Guard.AgainstNullOrWhiteSpace(country, nameof(country));

        return new Address(
            street.Trim(),
            city.Trim(),
            state.Trim(),
            postalCode.Trim().ToUpperInvariant(),
            country.Trim());
    }

    /// <summary>
    /// Gets the full address formatted for display or mailing.
    /// Example: "123 Main St, Springfield, IL 62701, USA"
    /// </summary>
    public string GetFullAddress()
    {
        return $"{Street}, {City}, {State} {PostalCode}, {Country}";
    }

    /// <summary>
    /// Returns the full address as a string.
    /// </summary>
    public override string ToString() => GetFullAddress();

    /// <summary>
    /// Gets the equality components for value object comparison.
    /// </summary>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street.ToLowerInvariant();
        yield return City.ToLowerInvariant();
        yield return State.ToLowerInvariant();
        yield return PostalCode;
        yield return Country.ToLowerInvariant();
    }
}