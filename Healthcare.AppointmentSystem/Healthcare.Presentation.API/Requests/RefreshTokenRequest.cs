namespace Healthcare.Presentation.API.Requests;

/// <summary>
/// Request model for refreshing an access token.
/// </summary>
public sealed class RefreshTokenRequest
{
    /// <summary>
    /// Gets or sets the refresh token.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
}
