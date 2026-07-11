using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Healthcare.Adapters.Authentication;
using Healthcare.Application.Ports.Authentication;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Common;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Adapters.Authentication;

/// <summary>
/// Security edge cases for JWT validation. Failures here are auth bypass / session hijack risks.
/// </summary>
public sealed class JwtSecurityEdgeCaseTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IBreachedPasswordChecker> _breach = new();

    public JwtSecurityEdgeCaseTests()
    {
        _uow.Setup(u => u.Users).Returns(_users.Object);
        _breach.Setup(b => b.IsPasswordBreachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private static User CreateUser(int id = 1)
    {
        var user = User.Create("secureuser", Email.Create("secure@test.com"), "hash", UserRole.Patient);
        typeof(Entity).GetProperty("Id")!.SetValue(user, id);
        return user;
    }

    private JwtAuthenticationService CreateService(JwtSettings settings) =>
        new(
            _uow.Object,
            _hasher.Object,
            _breach.Object,
            settings,
            Mock.Of<ILogger<JwtAuthenticationService>>(),
            redis: null);

    private static JwtSettings DefaultSettings(int expMinutes = 60, int skew = 0) => new()
    {
        Secret = "ProductionGradeSecretKeyAtLeast32Chars!!",
        Issuer = "HealthcareAPI",
        Audience = "HealthcareClients",
        ExpirationInMinutes = expMinutes,
        ClockSkewSeconds = skew,
        RefreshTokenExpirationInDays = 7
    };

    private static string ForgeToken(
        string secret,
        string issuer,
        string audience,
        DateTime expires,
        string alg = SecurityAlgorithms.HmacSha256,
        IEnumerable<Claim>? claims = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, alg);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims ?? new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(JwtRegisteredClaimNames.Sub, "1")
            },
            notBefore: DateTime.UtcNow.AddHours(-1),
            expires: expires,
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task ValidateTokenAsync_ExpiredToken_ReturnsFailure()
    {
        // Essential: expired access tokens must never authorize API calls.
        var settings = DefaultSettings(expMinutes: 60, skew: 0);
        var service = CreateService(settings);
        var token = ForgeToken(
            settings.Secret,
            settings.Issuer,
            settings.Audience,
            expires: DateTime.UtcNow.AddMinutes(-5));

        var result = await service.ValidateTokenAsync(token);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid or expired");
    }

    [Fact]
    public async Task ValidateTokenAsync_WrongIssuer_ReturnsFailure()
    {
        // Essential: tokens minted by another app/env must be rejected (issuer binding).
        var settings = DefaultSettings();
        var service = CreateService(settings);
        var token = ForgeToken(
            settings.Secret,
            issuer: "EvilIssuer",
            audience: settings.Audience,
            expires: DateTime.UtcNow.AddMinutes(30));

        var result = await service.ValidateTokenAsync(token);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateTokenAsync_WrongAudience_ReturnsFailure()
    {
        // Essential: audience restriction prevents token reuse across services.
        var settings = DefaultSettings();
        var service = CreateService(settings);
        var token = ForgeToken(
            settings.Secret,
            settings.Issuer,
            audience: "OtherService",
            expires: DateTime.UtcNow.AddMinutes(30));

        var result = await service.ValidateTokenAsync(token);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateTokenAsync_WrongSigningKey_ReturnsFailure()
    {
        // Essential: after secret rotation / attacker-forged token with different key must fail.
        var settings = DefaultSettings();
        var service = CreateService(settings);
        var token = ForgeToken(
            secret: "CompletelyDifferentSecretKeyAtLeast32Chars!",
            settings.Issuer,
            settings.Audience,
            expires: DateTime.UtcNow.AddMinutes(30));

        var result = await service.ValidateTokenAsync(token);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateTokenAsync_EmptyToken_ReturnsFailure()
    {
        var service = CreateService(DefaultSettings());
        (await service.ValidateTokenAsync("")).IsFailure.Should().BeTrue();
        (await service.ValidateTokenAsync("   ")).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void JwtSettings_FromConfiguration_MissingSecret_Throws()
    {
        // Essential: misconfigured production must fail fast at startup, not issue unsigned tokens.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "HealthcareAPI",
                ["Jwt:Secret"] = ""
            })
            .Build();

        var act = () => JwtSettings.FromConfiguration(config);
        act.Should().Throw<InvalidOperationException>().WithMessage("*not configured*");
    }

    [Fact]
    public void JwtSettings_FromConfiguration_ShortSecret_Throws()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "tooshort"
            })
            .Build();

        var act = () => JwtSettings.FromConfiguration(config);
        act.Should().Throw<InvalidOperationException>().WithMessage("*32*");
    }

    [Fact]
    public void JwtTokenValidation_Parameters_EnforceHs256AndLifetime()
    {
        // Essential: defense-in-depth against alg confusion and missing exp.
        var p = JwtTokenValidation.CreateParameters(DefaultSettings(skew: 30));
        p.RequireSignedTokens.Should().BeTrue();
        p.RequireExpirationTime.Should().BeTrue();
        p.ValidateIssuer.Should().BeTrue();
        p.ValidateAudience.Should().BeTrue();
        p.ValidAlgorithms.Should().Contain(SecurityAlgorithms.HmacSha256);
        p.ClockSkew.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task LoginAsync_ThenValidate_Succeeds_ForMintedToken()
    {
        var user = CreateUser();
        _users.Setup(r => r.GetByUsernameAsync("secureuser", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.VerifyPassword("password", user.PasswordHash)).Returns(true);

        var service = CreateService(DefaultSettings());
        var login = await service.LoginAsync("secureuser", "password");
        login.IsSuccess.Should().BeTrue();

        var validated = await service.ValidateTokenAsync(login.Value.AccessToken);
        validated.IsSuccess.Should().BeTrue();
        validated.Value.Should().Be(user.Id);
    }

    [Fact]
    public async Task ValidateTokenAsync_TokenSignedWithNoneAlgorithm_IsRejected()
    {
        // Essential: classic JWT "alg:none" / unsigned token attack must not validate.
        // Hand-build an unsigned payload and ensure ValidateToken fails.
        var header = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}"""));
        var payload = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(
            $$"""{"sub":"1","nameid":"1","exp":{{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()}}}"""));
        var unsigned = $"{header}.{payload}.";

        var service = CreateService(DefaultSettings());
        var result = await service.ValidateTokenAsync(unsigned);
        result.IsFailure.Should().BeTrue();
    }
}
