namespace Healthcare.Presentation.API.Requests;

/// <summary>
/// Request model for user registration.
/// </summary>
public sealed class RegisterRequest
{
    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    /// <example>john_doe</example>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email address.
    /// </summary>
    /// <example>john.doe@email.com</example>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password.
    /// </summary>
    /// <example>SecurePassword123!</example>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user role.
    /// </summary>
    /// <example>Patient</example>
    public string Role { get; set; } = "Patient";
}