using System.Security.Cryptography;

namespace Healthcare.Presentation.API.Services;

/// <summary>
/// Pure security policy for seeding decisions (testable without DI / database).
/// </summary>
public static class SeedingPolicy
{
    public const int MinimumPasswordLength = 12;

    /// <summary>
    /// Well-known / previously shipped credentials that must never be accepted.
    /// </summary>
    private static readonly HashSet<string> ForbiddenPasswords = new(StringComparer.Ordinal)
    {
        "Admin123!",
        "Admin123",
        "Password1!",
        "Password123!",
        "P@ssw0rd",
        "P@ssw0rd!",
        "Welcome1!",
        "ChangeMe123!",
        "Healthcare123!",
    };

    /// <summary>
    /// Demo data is allowed only when explicitly requested AND the host environment permits it.
    /// Production always returns false (defense in depth against misconfiguration).
    /// </summary>
    public static bool CanSeedDemoData(string environmentName, SeedingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.SeedDemoData)
            return false;

        if (IsProduction(environmentName))
            return false;

        if (IsDevelopment(environmentName))
            return true;

        // e.g. Docker / Staging demo boxes — require explicit opt-in
        return options.AllowDemoDataOutsideDevelopment;
    }

    /// <summary>
    /// Production may never invent credentials; non-Production may generate a one-time random password
    /// when bootstrap is enabled but no password was supplied via secrets.
    /// </summary>
    public static bool CanGenerateBootstrapPassword(string environmentName)
        => !IsProduction(environmentName);

    public static void EnsureStrongPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Bootstrap admin password is required. Set Seeding__BootstrapAdmin__Password " +
                "from a secret store or environment variable.");
        }

        // Check denylist first so legacy defaults (e.g. Admin123!) get a clear reason.
        if (ForbiddenPasswords.Contains(password))
        {
            throw new InvalidOperationException(
                "Bootstrap admin password is a known weak or previously default credential and is not allowed.");
        }

        if (password.Length < MinimumPasswordLength)
        {
            throw new InvalidOperationException(
                $"Bootstrap admin password must be at least {MinimumPasswordLength} characters.");
        }

        var hasUpper = password.Any(char.IsUpper);
        var hasLower = password.Any(char.IsLower);
        var hasDigit = password.Any(char.IsDigit);
        var hasNonAlphanumeric = password.Any(c => !char.IsLetterOrDigit(c));

        if (!hasUpper || !hasLower || !hasDigit || !hasNonAlphanumeric)
        {
            throw new InvalidOperationException(
                "Bootstrap admin password must include uppercase, lowercase, digit, and special characters.");
        }
    }

    /// <summary>
    /// Cryptographically secure random password suitable for one-time bootstrap logging in non-Production.
    /// </summary>
    public static string GenerateSecurePassword(int length = 24)
    {
        if (length < MinimumPasswordLength)
            length = MinimumPasswordLength;

        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%^&*-_=+";
        var all = upper + lower + digits + special;

        var chars = new char[length];
        chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        chars[3] = special[RandomNumberGenerator.GetInt32(special.Length)];

        for (var i = 4; i < length; i++)
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];

        // Fisher–Yates shuffle
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    public static bool IsProduction(string environmentName)
        => string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);

    public static bool IsDevelopment(string environmentName)
        => string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);

    public static string DescribeDemoBlockReason(string environmentName, SeedingOptions options)
    {
        if (!options.SeedDemoData)
            return "Seeding:SeedDemoData is false.";

        if (IsProduction(environmentName))
            return "Demo data is never seeded in Production.";

        if (!IsDevelopment(environmentName) && !options.AllowDemoDataOutsideDevelopment)
        {
            return $"Environment '{environmentName}' is not Development and " +
                   "Seeding:AllowDemoDataOutsideDevelopment is false.";
        }

        return "Demo seeding is allowed.";
    }
}
