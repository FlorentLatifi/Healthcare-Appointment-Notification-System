using Healthcare.Application.Ports.Authentication;
using Konscious.Security.Cryptography;
using System.Security.Cryptography;
using System.Text;

namespace Healthcare.Adapters.Authentication;

/// <summary>
/// OWASP-aligned Argon2id password hasher with transparent BCrypt verify + upgrade path.
/// Parameters (m=19456 KiB, t=2, p=1) match OWASP 2023 "second recommendation" for interactive logins.
/// </summary>
public sealed class Argon2IdPasswordHasher : IPasswordHasher
{
    private const string Argon2IdPrefix = "$argon2id$";
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int DegreeOfParallelism = 1;
    private const int MemorySize = 19456; // KiB ≈ 19 MiB
    private const int Iterations = 2;

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = HashArgon2id(password, salt);
        return FormatHash(salt, hash);
    }

    public bool VerifyPassword(string password, string hash)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentException.ThrowIfNullOrEmpty(hash);

        if (hash.StartsWith(Argon2IdPrefix, StringComparison.Ordinal))
        {
            var (salt, storedHash) = ParseHash(hash);
            var computedHash = HashArgon2id(password, salt);
            return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
        }

        // Legacy BCrypt hashes — constant-time verification via BCrypt library.
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }

    public bool RequiresRehash(string passwordHash)
    {
        if (!passwordHash.StartsWith(Argon2IdPrefix, StringComparison.Ordinal))
            return true; // BCrypt or unknown → upgrade to Argon2id

        // Rehash if parameters drifted from current policy (e.g. after OWASP bump).
        return !passwordHash.Contains($"m={MemorySize},t={Iterations},p={DegreeOfParallelism}", StringComparison.Ordinal);
    }

    private static byte[] HashArgon2id(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = DegreeOfParallelism,
            MemorySize = MemorySize,
            Iterations = Iterations
        };
        return argon2.GetBytes(HashSize);
    }

    private static string FormatHash(byte[] salt, byte[] hash)
    {
        return $"{Argon2IdPrefix}v=19$m={MemorySize},t={Iterations},p={DegreeOfParallelism}$" +
               $"{Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static (byte[] Salt, byte[] Hash) ParseHash(string hash)
    {
        // Format: $argon2id$v=19$m=...,t=...,p=...$salt$hash
        var segments = hash.Substring(Argon2IdPrefix.Length).Split('$');
        if (segments.Length < 4)
            throw new FormatException("Invalid Argon2id hash format.");

        var salt = Convert.FromBase64String(segments[2]);
        var hashBytes = Convert.FromBase64String(segments[3]);
        return (salt, hashBytes);
    }
}
