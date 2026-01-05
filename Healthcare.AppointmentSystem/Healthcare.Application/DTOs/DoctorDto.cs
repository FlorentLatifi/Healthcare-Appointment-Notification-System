namespace Healthcare.Application.DTOs;

/// <summary>
/// Data Transfer Object for Doctor entity.
/// </summary>
public sealed class DoctorDto
{
    /// <summary>
    /// Gets or sets the doctor ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the doctor's first name.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the doctor's last name.
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the doctor's full name (with title).
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the doctor's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the doctor's phone number.
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the doctor's license number.
    /// </summary>
    public string LicenseNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the doctor's specialties.
    /// </summary>
    public List<string> Specialties { get; set; } = new();

    /// <summary>
    /// Gets or sets the consultation fee amount.
    /// </summary>
    public decimal ConsultationFeeAmount { get; set; }

    /// <summary>
    /// Gets or sets the consultation fee currency.
    /// </summary>
    public string ConsultationFeeCurrency { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the doctor is accepting patients.
    /// </summary>
    public bool IsAcceptingPatients { get; set; }

    /// <summary>
    /// Gets or sets whether the doctor is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the doctor's years of experience.
    /// </summary>
    public int YearsOfExperience { get; set; }

    /// <summary>
    /// Gets or sets when the doctor was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}