using Healthcare.Application.Ports.Authentication;

namespace Healthcare.Adapters.Authentication;

/// <summary>
/// BCrypt implementation of password hashing.
/// </summary>
/// <remarks>
/// Design Pattern: Adapter Pattern
/// 
/// Uses BCrypt for secure password hashing.
/// Can be replaced with another algorithm without touching Application layer.
/// </remarks>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}