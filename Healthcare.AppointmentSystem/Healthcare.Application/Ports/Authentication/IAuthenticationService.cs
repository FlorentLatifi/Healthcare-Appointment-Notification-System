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
    /// Authenticates a user and returns a token.
    /// </summary>
    Task<Result<string>> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a token and returns user ID.
    /// </summary>
    Task<Result<int>> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default);
}