namespace Healthcare.Application.Ports.Authentication;

/// <summary>
/// PORT for password hashing.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a plain text password.
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Verifies if a password matches a hash.
    /// </summary>
    bool VerifyPassword(string password, string hash);
}