namespace Healthcare.Presentation.API.Responses;

/// <summary>
/// Response containing JWT token after successful login.
/// </summary>
/// <remarks>
/// The refresh token is not included in the JSON body — it is set as an
/// httpOnly cookie (Secure when HTTPS, SameSite=Lax, Path=/api/v1) by the
/// controller, so it is never accessible to JavaScript and is sent on
/// same-site credentialed requests (including /Auth/refresh).
/// </remarks>
public sealed class LoginResponse
{
    /// <summary>
    /// Gets or sets the JWT access token.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the token expiration time.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user role.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the patient ID if the user has a linked patient profile.
    /// </summary>
    public int? PatientId { get; set; }

    /// <summary>
    /// Gets or sets the doctor ID if the user has a linked doctor profile.
    /// </summary>
    public int? DoctorId { get; set; }
}