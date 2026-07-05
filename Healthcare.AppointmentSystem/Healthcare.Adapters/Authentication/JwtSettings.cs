using Microsoft.Extensions.Configuration;

namespace Healthcare.Adapters.Authentication;

/// <summary>
/// JWT configuration settings.
/// </summary>
public sealed class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpirationInMinutes { get; set; } = 60;
    public int RefreshTokenExpirationInDays { get; set; } = 7;

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
            ExpirationInMinutes = int.Parse(configuration["Jwt:ExpirationInMinutes"] ?? "60"),
            RefreshTokenExpirationInDays = int.Parse(configuration["Jwt:RefreshTokenExpirationInDays"] ?? "7")
        };
    }
}