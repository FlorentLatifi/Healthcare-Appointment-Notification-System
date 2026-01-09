namespace Healthcare.Presentation.API.Requests;

/// <summary>
/// Request model for user login.
/// </summary>
public sealed class LoginRequest
{
    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    /// <example>john_doe</example>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password.
    /// </summary>
    /// <example>SecurePassword123!</example>
    public string Password { get; set; } = string.Empty;
}