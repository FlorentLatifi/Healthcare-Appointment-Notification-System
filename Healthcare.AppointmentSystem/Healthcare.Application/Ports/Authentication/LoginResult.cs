using System;

namespace Healthcare.Application.Ports.Authentication;

/// <summary>
/// Result object containing the Access Token, Refresh Token, and access token expiration time.
/// </summary>
public sealed class LoginResult
{
    /// <summary>
    /// Gets or sets the access token (JWT).
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the refresh token.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the access token expiration time.
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}
