namespace Healthcare.Presentation.API.Responses;

/// <summary>
/// Response containing JWT token after successful login.
/// </summary>
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
}