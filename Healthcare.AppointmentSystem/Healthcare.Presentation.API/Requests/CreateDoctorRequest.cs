namespace Healthcare.Presentation.API.Requests;

/// <summary>
/// Request model for creating a new doctor.
/// </summary>
public sealed class CreateDoctorRequest
{
    /// <summary>
    /// Gets or sets the doctor's first name.
    /// </summary>
    /// <example>Jane</example>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the doctor's last name.
    /// </summary>
    /// <example>Smith</example>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the doctor's email address.
    /// </summary>
    /// <example>dr.smith@healthcareclinic.com</example>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the doctor's phone number.
    /// </summary>
    /// <example>+38349987654</example>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the doctor's medical license number.
    /// </summary>
    /// <example>MED-12345</example>
    public string LicenseNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the doctor's primary specialty.
    /// </summary>
    /// <example>Cardiology</example>
    public string Specialty { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the consultation fee amount.
    /// </summary>
    /// <example>50.00</example>
    public decimal ConsultationFeeAmount { get; set; }

    /// <summary>
    /// Gets or sets the consultation fee currency.
    /// </summary>
    /// <example>USD</example>
    public string ConsultationFeeCurrency { get; set; } = "USD";

    /// <summary>
    /// Gets or sets the doctor's years of experience.
    /// </summary>
    /// <example>10</example>
    public int YearsOfExperience { get; set; }
}