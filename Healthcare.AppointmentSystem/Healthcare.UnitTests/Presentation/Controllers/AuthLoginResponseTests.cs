using FluentAssertions;
using Healthcare.Application.Ports.Authentication;
using Healthcare.Presentation.API.Controllers;

namespace Healthcare.UnitTests.Presentation.Controllers;

/// <summary>
/// Unit tests for AuthController.BuildLoginResponse — guards the empty-role SPA bug.
/// </summary>
public sealed class AuthLoginResponseTests
{
    [Fact]
    public void BuildLoginResponse_UsesDomainIdentity_EvenWhenTokenHasOnlyShortClaimNames()
    {
        var result = new LoginResult
        {
            AccessToken = CreateMinimalJwtWithShortClaims(),
            RefreshToken = "refresh",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            FamilyId = Guid.NewGuid(),
            UserId = 9,
            Username = "jane",
            Role = "Patient",
            PatientId = 42,
            DoctorId = null,
        };

        var response = AuthController.BuildLoginResponse(result);

        response.Token.Should().Be(result.AccessToken);
        response.Username.Should().Be("jane");
        response.Role.Should().Be("Patient");
        response.PatientId.Should().Be(42);
        response.DoctorId.Should().BeNull();
    }

    [Fact]
    public void BuildLoginResponse_FallsBackToShortJwtClaims_WhenDomainIdentityMissing()
    {
        var result = new LoginResult
        {
            AccessToken = CreateMinimalJwtWithShortClaims(),
            RefreshToken = "refresh",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            FamilyId = Guid.NewGuid(),
            // Intentionally empty — forces JWT claim fallback path
            Username = "",
            Role = "",
        };

        var response = AuthController.BuildLoginResponse(result);

        response.Username.Should().Be("jwtuser");
        response.Role.Should().Be("Doctor");
        response.PatientId.Should().BeNull();
        response.DoctorId.Should().Be(7);
    }

    [Fact]
    public void BuildLoginResponse_NormalizesZeroProfileIdsToNull()
    {
        var result = new LoginResult
        {
            AccessToken = "not-used-when-identity-present",
            Username = "admin",
            Role = "Admin",
            PatientId = 0,
            DoctorId = 0,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
        };

        var response = AuthController.BuildLoginResponse(result);

        response.Role.Should().Be("Admin");
        response.PatientId.Should().BeNull();
        response.DoctorId.Should().BeNull();
    }

    /// <summary>
    /// Builds a JWT whose payload uses short claim types (role, unique_name) —
    /// the form written by JwtSecurityTokenHandler outbound mapping.
    /// </summary>
    private static string CreateMinimalJwtWithShortClaims()
    {
        // Header.payload.sig — only claims matter for ReadJwtToken in BuildLoginResponse fallback.
        // Use a real signed token so the handler can parse it.
        var key = System.Text.Encoding.UTF8.GetBytes("UnitTestSecretKey_AtLeast32Characters!");
        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "test",
            audience: "test",
            claims:
            [
                new System.Security.Claims.Claim("unique_name", "jwtuser"),
                new System.Security.Claims.Claim("role", "Doctor"),
                new System.Security.Claims.Claim("doctor_id", "7"),
            ],
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }
}
