namespace Healthcare.Presentation.API.Responses;

/// <summary>
/// Response containing JWT token after successful login.
/// </summary>
/// <remarks>
/// The refresh token is not included in the JSON body — it is set as an
/// httpOnly, Secure, SameSite=Strict cookie by the controller, so it is
/// never accessible to JavaScript and is sent automatically on subsequent
/// requests (including the /refresh endpoint).
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
}