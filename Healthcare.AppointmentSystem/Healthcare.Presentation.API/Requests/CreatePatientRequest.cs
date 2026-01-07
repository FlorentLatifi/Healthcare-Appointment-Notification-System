namespace Healthcare.Presentation.API.Requests;

/// <summary>
/// Request model for creating a new patient.
/// </summary>
public sealed class CreatePatientRequest
{
    /// <summary>
    /// Gets or sets the patient's first name.
    /// </summary>
    /// <example>John</example>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the patient's last name.
    /// </summary>
    /// <example>Doe</example>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the patient's email address.
    /// </summary>
    /// <example>john.doe@email.com</example>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the patient's phone number.
    /// </summary>
    /// <example>+38349123456</example>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the patient's date of birth.
    /// </summary>
    /// <example>1990-05-15</example>
    public DateTime DateOfBirth { get; set; }

    /// <summary>
    /// Gets or sets the patient's gender.
    /// </summary>
    /// <example>Male</example>
    public string Gender { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the street address.
    /// </summary>
    /// <example>123 Main Street</example>
    public string Street { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the city.
    /// </summary>
    /// <example>Pristina</example>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the state or province.
    /// </summary>
    /// <example>Kosovo</example>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the postal code.
    /// </summary>
    /// <example>10000</example>
    public string PostalCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the country.
    /// </summary>
    /// <example>Kosovo</example>
    public string Country { get; set; } = string.Empty;
}