namespace Healthcare.Application.DTOs;

/// <summary>
/// Data Transfer Object for Patient entity.
/// </summary>
/// <remarks>
/// DTOs are used to transfer data between layers without exposing domain entities.
/// This DTO contains only the data needed for API responses.
/// </remarks>
public sealed class PatientDto
{
    /// <summary>
    /// Gets or sets the patient ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the patient's first name.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the patient's last name.
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the patient's full name.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the patient's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the patient's phone number.
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the patient's date of birth.
    /// </summary>
    public DateTime DateOfBirth { get; set; }

    /// <summary>
    /// Gets or sets the patient's age.
    /// </summary>
    public int Age { get; set; }

    /// <summary>
    /// Gets or sets the patient's gender.
    /// </summary>
    public string Gender { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the patient's address.
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the patient is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets when the patient was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}