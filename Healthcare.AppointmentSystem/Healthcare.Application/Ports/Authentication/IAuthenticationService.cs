using Healthcare.Application.Common;

namespace Healthcare.Application.Ports.Authentication;

/// <summary>
/// PORT for authentication services.
/// </summary>
/// <remarks>
/// Design Pattern: Port (Hexagonal Architecture)
/// 
/// This interface defines WHAT authentication can do,
/// without knowing HOW it's implemented (JWT, OAuth, etc.)
/// </remarks>
public interface IAuthenticationService
{
    /// <summary>
    /// Registers a new user.
    /// </summary>
    Task<Result<int>> RegisterAsync(
        string username,
        string email,
        string password,
        string role,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates a user and returns a token set (access & refresh token).
    /// </summary>
    Task<Result<LoginResult>> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the access token using a valid refresh token.
    /// </summary>
    Task<Result<LoginResult>> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a refresh token, logging the user out.
    /// </summary>
    Task<Result> RevokeTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a token and returns user ID.
    /// </summary>
    Task<Result<int>> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all refresh tokens in the given family and marks the UserSession as revoked.
    /// </summary>
    Task<Result> RevokeFamilyAsync(Guid familyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all active sessions for a user (logs out everywhere).
    /// </summary>
    Task<Result> RevokeAllUserSessionsAsync(int userId, CancellationToken cancellationToken = default);
}