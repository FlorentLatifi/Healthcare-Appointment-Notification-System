using Healthcare.Application.Common;

namespace Healthcare.Application.Ports.Authentication;

/// <summary>
/// PORT for authentication services.
/// </summary>
/// <remarks>
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
    /// Re-issues an access token for an existing user from current DB state (e.g. after
    /// linking PatientId / DoctorId). Does not rotate the refresh-token cookie — use
    /// <see cref="RefreshTokenAsync"/> for full session rotation.
    /// </summary>
    Task<Result<LoginResult>> IssueAccessTokenForUserAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a refresh token and its rotation family (single-device logout).
    /// Returns the revoked family id when known (for UserSession cleanup).
    /// </summary>
    Task<Result<Guid?>> RevokeTokenAsync(
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

    /// <summary>
    /// Generates a short-lived single-use password reset token for the given user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw reset token to include in the email link.</returns>
    Task<string> GeneratePasswordResetTokenAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and consumes a password reset token.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="token">The raw token from the reset link.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success if the token is valid and consumed; failure otherwise.</returns>
    Task<Result> ValidateAndConsumePasswordResetTokenAsync(int userId, string token, CancellationToken cancellationToken = default);
}