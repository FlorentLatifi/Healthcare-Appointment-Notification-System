using System.Security.Claims;
using System.Text;
using Healthcare.Application.Ports.Authentication;
using Microsoft.IdentityModel.Tokens;

namespace Healthcare.Adapters.Authentication;

/// <summary>
/// Builds hardened <see cref="TokenValidationParameters"/> for access tokens (HS256 only).
/// Shared by JWT Bearer middleware configuration and manual validation.
/// </summary>
public static class JwtTokenValidation
{
    public static TokenValidationParameters CreateParameters(JwtSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidIssuer = settings.Issuer,
            ValidAudience = settings.Audience,
            // Explicit algorithm allow-list mitigates alg confusion / "none" attacks.
            // Accept both short ("HS256") and long-form algorithm identifiers used by libraries.
            ValidAlgorithms = new[]
            {
                SecurityAlgorithms.HmacSha256,
                SecurityAlgorithms.HmacSha256Signature
            },
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret)),
            ClockSkew = TimeSpan.FromSeconds(Math.Clamp(settings.ClockSkewSeconds, 0, 300)),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
    }
}
