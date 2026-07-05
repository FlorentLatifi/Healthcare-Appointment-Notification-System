using FluentAssertions;
using Healthcare.Adapters.Authentication;
using Healthcare.Application.Common;
using Healthcare.Application.Ports.Authentication;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Common;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace Healthcare.UnitTests.Adapters.Authentication;

public sealed class JwtAuthenticationServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<ILogger<JwtAuthenticationService>> _loggerMock;
    private readonly JwtSettings _jwtSettings;
    private readonly JwtAuthenticationService _service;

    public JwtAuthenticationServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _userRepoMock = new Mock<IUserRepository>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _loggerMock = new Mock<ILogger<JwtAuthenticationService>>();

        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);

        _jwtSettings = new JwtSettings
        {
            Secret = "ThisIsATestSecretKeyThatIsAtLeast32Characters!",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpirationInMinutes = 60,
            RefreshTokenExpirationInDays = 7
        };

        _service = new JwtAuthenticationService(
            _unitOfWorkMock.Object,
            _passwordHasherMock.Object,
            _jwtSettings,
            _loggerMock.Object,
            redis: null);
    }

    private static User CreateTestUser(int id = 1, string username = "testuser", bool isActive = true)
    {
        var user = User.Create(username, Email.Create("test@example.com"), "hashedPassword", UserRole.Patient);

        var idProperty = typeof(Entity).GetProperty("Id",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        idProperty?.SetValue(user, id);

        if (!isActive)
        {
            user.Deactivate();
        }

        return user;
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsLoginResultWithTokens()
    {
        var user = CreateTestUser();
        _userRepoMock.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(p => p.VerifyPassword("password", user.PasswordHash))
            .Returns(true);

        var result = await _service.LoginAsync("testuser", "password");

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.Value.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.Value.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(60), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task LoginAsync_InvalidPassword_ReturnsFailure()
    {
        var user = CreateTestUser();
        _userRepoMock.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(p => p.VerifyPassword("wrongpassword", user.PasswordHash))
            .Returns(false);

        var result = await _service.LoginAsync("testuser", "wrongpassword");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid username or password");
    }

    [Fact]
    public async Task LoginAsync_DeactivatedUser_ReturnsFailure()
    {
        var user = CreateTestUser(isActive: false);
        _userRepoMock.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(p => p.VerifyPassword("password", user.PasswordHash))
            .Returns(true);

        var result = await _service.LoginAsync("testuser", "password");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("deactivated");
    }

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_ReturnsNewTokens()
    {
        var user = CreateTestUser();
        _userRepoMock.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(p => p.VerifyPassword("password", user.PasswordHash))
            .Returns(true);
        _userRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var loginResult = await _service.LoginAsync("testuser", "password");
        var refreshToken = loginResult.Value.RefreshToken;

        var refreshResult = await _service.RefreshTokenAsync(refreshToken);

        refreshResult.IsSuccess.Should().BeTrue();
        refreshResult.Value.AccessToken.Should().NotBeNullOrWhiteSpace();
        refreshResult.Value.RefreshToken.Should().NotBeNullOrWhiteSpace();
        refreshResult.Value.RefreshToken.Should().NotBe(refreshToken);
    }

    [Fact]
    public async Task RefreshTokenAsync_ReusedToken_ReturnsFailure()
    {
        var user = CreateTestUser();
        _userRepoMock.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(p => p.VerifyPassword("password", user.PasswordHash))
            .Returns(true);
        _userRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var loginResult = await _service.LoginAsync("testuser", "password");
        var refreshToken = loginResult.Value.RefreshToken;

        await _service.RefreshTokenAsync(refreshToken);

        var secondAttempt = await _service.RefreshTokenAsync(refreshToken);

        secondAttempt.IsFailure.Should().BeTrue();
        secondAttempt.Error.Should().Contain("Invalid or expired");
    }

    [Fact]
    public async Task RefreshTokenAsync_InvalidToken_ReturnsFailure()
    {
        var result = await _service.RefreshTokenAsync("invalid-refresh-token");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid or expired");
    }

    [Fact]
    public async Task RevokeTokenAsync_ValidToken_TokenCannotBeUsedAfterRevoke()
    {
        var user = CreateTestUser();
        _userRepoMock.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(p => p.VerifyPassword("password", user.PasswordHash))
            .Returns(true);

        var loginResult = await _service.LoginAsync("testuser", "password");
        var refreshToken = loginResult.Value.RefreshToken;

        var revokeResult = await _service.RevokeTokenAsync(refreshToken);

        revokeResult.IsSuccess.Should().BeTrue();

        var refreshAttempt = await _service.RefreshTokenAsync(refreshToken);
        refreshAttempt.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeTokenAsync_InvalidToken_ReturnsSuccess()
    {
        var result = await _service.RevokeTokenAsync("non-existent-token");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshTokenAsync_Rotation_OldTokenInvalidAfterNewTokenIssued()
    {
        var user = CreateTestUser();
        _userRepoMock.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(p => p.VerifyPassword("password", user.PasswordHash))
            .Returns(true);
        _userRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var loginResult = await _service.LoginAsync("testuser", "password");
        var oldRefreshToken = loginResult.Value.RefreshToken;

        var refreshResult = await _service.RefreshTokenAsync(oldRefreshToken);
        var newRefreshToken = refreshResult.Value.RefreshToken;

        var reuseOld = await _service.RefreshTokenAsync(oldRefreshToken);
        reuseOld.IsFailure.Should().BeTrue("old token should be invalid after rotation");

        var reuseNew = await _service.RefreshTokenAsync(newRefreshToken);
        reuseNew.IsSuccess.Should().BeTrue("new token should still be valid");
    }
}
