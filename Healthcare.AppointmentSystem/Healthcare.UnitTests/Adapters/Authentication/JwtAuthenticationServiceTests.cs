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
using StackExchange.Redis;

namespace Healthcare.UnitTests.Adapters.Authentication;

public sealed class JwtAuthenticationServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<IBreachedPasswordChecker> _breachedPasswordCheckerMock;
    private readonly Mock<ILogger<JwtAuthenticationService>> _loggerMock;
    private readonly JwtSettings _jwtSettings;
    private readonly JwtAuthenticationService _service;

    public JwtAuthenticationServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _userRepoMock = new Mock<IUserRepository>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _breachedPasswordCheckerMock = new Mock<IBreachedPasswordChecker>();
        _breachedPasswordCheckerMock
            .Setup(x => x.IsPasswordBreachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
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
            _breachedPasswordCheckerMock.Object,
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
        // SPA session must receive identity from domain fields (not JWT claim remapping).
        result.Value.UserId.Should().Be(1);
        result.Value.Username.Should().Be("testuser");
        result.Value.Role.Should().Be("Patient");
        result.Value.PatientId.Should().BeNull();
        result.Value.DoctorId.Should().BeNull();
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
        revokeResult.Value.Should().Be(loginResult.Value.FamilyId);

        var refreshAttempt = await _service.RefreshTokenAsync(refreshToken);
        refreshAttempt.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeTokenAsync_InvalidToken_ReturnsSuccessWithNullFamily()
    {
        var result = await _service.RevokeTokenAsync("non-existent-token");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task ValidateTokenAsync_ValidAccessToken_ReturnsUserId()
    {
        var user = CreateTestUser();
        _userRepoMock.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(p => p.VerifyPassword("password", user.PasswordHash))
            .Returns(true);

        var login = await _service.LoginAsync("testuser", "password");
        login.IsSuccess.Should().BeTrue();
        var result = await _service.ValidateTokenAsync(login.Value.AccessToken);

        result.IsSuccess.Should().BeTrue($"validation error: {result.Error}; token={login.Value.AccessToken}");
        result.Value.Should().Be(user.Id);
    }

    [Fact]
    public async Task ValidateTokenAsync_TamperedToken_ReturnsFailureWithoutThrowing()
    {
        var result = await _service.ValidateTokenAsync("not.a.jwt");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid or expired");
    }

    [Fact]
    public async Task RefreshTokenAsync_Rotation_ReuseRevokesEntireFamily()
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
        reuseNew.IsFailure.Should().BeTrue("new token should also be invalid because family was revoked on reuse");
    }

    [Fact]
    public async Task LoginAsync_ReturnsFamilyId()
    {
        var user = CreateTestUser();
        _userRepoMock.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(p => p.VerifyPassword("password", user.PasswordHash))
            .Returns(true);

        var result = await _service.LoginAsync("testuser", "password");

        result.IsSuccess.Should().BeTrue();
        result.Value.FamilyId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task RefreshTokenAsync_MaintainsFamilyId()
    {
        var user = CreateTestUser();
        _userRepoMock.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(p => p.VerifyPassword("password", user.PasswordHash))
            .Returns(true);
        _userRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var loginResult = await _service.LoginAsync("testuser", "password");
        var originalFamilyId = loginResult.Value.FamilyId;

        var refreshResult = await _service.RefreshTokenAsync(loginResult.Value.RefreshToken);

        refreshResult.IsSuccess.Should().BeTrue();
        refreshResult.Value.FamilyId.Should().Be(originalFamilyId);
    }

    [Fact]
    public async Task RevokeFamilyAsync_BlocksSubsequentRefresh()
    {
        var user = CreateTestUser();
        _userRepoMock.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(p => p.VerifyPassword("password", user.PasswordHash))
            .Returns(true);
        _userRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var loginResult = await _service.LoginAsync("testuser", "password");
        var familyId = loginResult.Value.FamilyId;

        await _service.RevokeFamilyAsync(familyId);

        var refreshResult = await _service.RefreshTokenAsync(loginResult.Value.RefreshToken);
        refreshResult.IsFailure.Should().BeTrue();
        refreshResult.Error.Should().Contain("Session has been revoked");
    }

    [Fact]
    public async Task RevokeAllUserSessionsAsync_RevokesAllActiveSessions()
    {
        var userSessionRepoMock = new Mock<IUserSessionRepository>();
        _unitOfWorkMock.Setup(u => u.UserSessions).Returns(userSessionRepoMock.Object);

        var user = CreateTestUser();
        var family1 = Guid.NewGuid();
        var family2 = Guid.NewGuid();

        var sessions = new List<UserSession>
        {
            new(user.Id, family1, "Agent1", "127.0.0.1"),
            new(user.Id, family2, "Agent2", "127.0.0.2")
        };

        userSessionRepoMock.Setup(r => r.GetActiveByUserIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var result = await _service.RevokeAllUserSessionsAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        sessions.All(s => s.IsRevoked).Should().BeTrue();
        userSessionRepoMock.Verify(r => r.UpdateAsync(It.IsAny<UserSession>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task LoginAsync_RedisConnectionExceptionOnStore_PropagatesException()
    {
        var dbMock = new Mock<IDatabase>(MockBehavior.Loose);
        dbMock.Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<Expiration>(),
                It.IsAny<ValueCondition>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect,
                "No connection could be made to the Redis server"));

        var redisMock = new Mock<IConnectionMultiplexer>(MockBehavior.Loose);
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(dbMock.Object);

        var serviceWithRedis = new JwtAuthenticationService(
            _unitOfWorkMock.Object,
            _passwordHasherMock.Object,
            _breachedPasswordCheckerMock.Object,
            _jwtSettings,
            _loggerMock.Object,
            redisMock.Object);

        var user = CreateTestUser();
        _userRepoMock.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(p => p.VerifyPassword("password", user.PasswordHash))
            .Returns(true);

        var act = () => serviceWithRedis.LoginAsync("testuser", "password");

        await act.Should().ThrowAsync<RedisConnectionException>();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v!.ToString()!.Contains("Redis unavailable for refresh-token storage")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task RefreshTokenAsync_RedisConnectionExceptionOnConsume_PropagatesException()
    {
        var dbMock = new Mock<IDatabase>(MockBehavior.Loose);
        dbMock.Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect,
                "No connection could be made to the Redis server"));

        var redisMock = new Mock<IConnectionMultiplexer>(MockBehavior.Loose);
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(dbMock.Object);

        var serviceWithRedis = new JwtAuthenticationService(
            _unitOfWorkMock.Object,
            _passwordHasherMock.Object,
            _breachedPasswordCheckerMock.Object,
            _jwtSettings,
            _loggerMock.Object,
            redisMock.Object);

        var act = () => serviceWithRedis.RefreshTokenAsync("some-refresh-token");

        await act.Should().ThrowAsync<RedisConnectionException>();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v!.ToString()!.Contains("Redis unavailable for refresh-token storage")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task RevokeTokenAsync_RedisConnectionExceptionOnDelete_PropagatesException()
    {
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(d => d.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect,
                "No connection could be made to the Redis server"));
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(dbMock.Object);

        var serviceWithRedis = new JwtAuthenticationService(
            _unitOfWorkMock.Object,
            _passwordHasherMock.Object,
            _breachedPasswordCheckerMock.Object,
            _jwtSettings,
            _loggerMock.Object,
            redisMock.Object);

        var act = () => serviceWithRedis.RevokeTokenAsync("some-refresh-token");

        await act.Should().ThrowAsync<RedisConnectionException>();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v!.ToString()!.Contains("Redis unavailable for refresh-token storage")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
