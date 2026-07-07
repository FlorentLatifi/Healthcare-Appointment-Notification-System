using System;

namespace Healthcare.Application.Ports.Authentication;

/// <summary>
/// Result object containing the Access Token, Refresh Token, access token expiration time, and the session family ID.
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

    /// <summary>
    /// Gets or sets the family ID for session tracking.
    /// </summary>
    public Guid FamilyId { get; set; }
}
