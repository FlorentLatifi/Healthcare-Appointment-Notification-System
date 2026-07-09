using Healthcare.Application.Ports.Authentication;
using Konscious.Security.Cryptography;
using System.Security.Cryptography;
using System.Text;

namespace Healthcare.Adapters.Authentication;

public sealed class Argon2IdPasswordHasher : IPasswordHasher
{
    private const string Argon2IdPrefix = "$argon2id$";
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int DegreeOfParallelism = 1;
    private const int MemorySize = 19456;
    private const int Iterations = 2;

    public string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = HashArgon2id(password, salt);
        return FormatHash(salt, hash);
    }

    public bool VerifyPassword(string password, string hash)
    {
        if (hash.StartsWith(Argon2IdPrefix, StringComparison.Ordinal))
        {
            var (salt, storedHash) = ParseHash(hash);
            var computedHash = HashArgon2id(password, salt);
            return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
        }

        return BCrypt.Net.BCrypt.Verify(password, hash);
    }

    public bool RequiresRehash(string passwordHash)
    {
        return !passwordHash.StartsWith(Argon2IdPrefix, StringComparison.Ordinal);
    }

    private static byte[] HashArgon2id(string password, byte[] salt)
    {
        var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
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
        var sb = new StringBuilder();
        sb.Append(Argon2IdPrefix);
        sb.Append("v=19$");
        sb.Append("m=").Append(MemorySize).Append(',');
        sb.Append("t=").Append(Iterations).Append(',');
        sb.Append("p=").Append(DegreeOfParallelism).Append('$');
        sb.Append(Convert.ToBase64String(salt)).Append('$');
        sb.Append(Convert.ToBase64String(hash));
        return sb.ToString();
    }

    private static (byte[] Salt, byte[] Hash) ParseHash(string hash)
    {
        var segments = hash.Substring(Argon2IdPrefix.Length).Split('$');
        var salt = Convert.FromBase64String(segments[2]);
        var hashBytes = Convert.FromBase64String(segments[3]);
        return (salt, hashBytes);
    }
}
