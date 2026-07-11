using Microsoft.Extensions.Configuration;

namespace Healthcare.Application.Ports.Authentication;

/// <summary>
/// JWT configuration settings (Application options).
/// Bound at the composition root; used by Presentation and Adapters.
/// </summary>
public sealed class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    /// <summary>Access-token lifetime. Prefer 5–15 minutes for PHI-bearing APIs.</summary>
    public int ExpirationInMinutes { get; set; } = 15;

    public int RefreshTokenExpirationInDays { get; set; } = 7;
    public int ResetTokenExpirationInMinutes { get; set; } = 60;

    /// <summary>
    /// Allowed clock skew for lifetime validation (default 60s). Keep low to limit stolen-token window.
    /// </summary>
    public int ClockSkewSeconds { get; set; } = 60;

    /// <summary>
    /// Binds JWT settings from configuration. Fails fast if the secret is missing or too short.
    /// </summary>
    public static JwtSettings FromConfiguration(IConfiguration configuration)
    {
        var secret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                "JWT secret is not configured. Set 'Jwt:Secret' via environment variables, " +
                "dotnet user-secrets (development), or a secure configuration provider.");
        }

        if (secret.Length < 32)
        {
            throw new InvalidOperationException(
                "JWT secret must be at least 32 characters long for HS256.");
        }

        return new JwtSettings
        {
            Secret = secret,
            Issuer = configuration["Jwt:Issuer"] ?? "HealthcareAPI",
            Audience = configuration["Jwt:Audience"] ?? "HealthcareClients",
            ExpirationInMinutes = int.Parse(configuration["Jwt:ExpirationInMinutes"] ?? "15"),
            RefreshTokenExpirationInDays = int.Parse(configuration["Jwt:RefreshTokenExpirationInDays"] ?? "7"),
            ResetTokenExpirationInMinutes = int.Parse(configuration["Jwt:ResetTokenExpirationInMinutes"] ?? "60"),
            ClockSkewSeconds = int.Parse(configuration["Jwt:ClockSkewSeconds"] ?? "60")
        };
    }
}
