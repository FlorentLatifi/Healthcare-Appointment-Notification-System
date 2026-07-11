using Healthcare.Adapters.Authentication;
using FluentAssertions;
using Healthcare.Application.Ports.Authentication;
using Healthcare.Adapters.Persistence.InMemory;
using Healthcare.Application.Ports.Authentication;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace Healthcare.UnitTests.Adapters.Authentication;

public sealed class SessionIntegrationTests
{
    private readonly InMemoryUserSessionRepository _sessionRepo;
    private readonly InMemoryAppointmentRepository _appointmentRepo;
    private readonly InMemoryPatientRepository _patientRepo;
    private readonly InMemoryDoctorRepository _doctorRepo;
    private readonly InMemoryUserRepository _userRepo;
    private readonly InMemoryPaymentRepository _paymentRepo;
    private readonly InMemoryAuditLogRepository _auditLogRepo;
    private readonly InMemoryUnitOfWork _unitOfWork;
    private readonly BcryptPasswordHasher _passwordHasher;
    private readonly Mock<IBreachedPasswordChecker> _breachedPasswordCheckerMock;
    private readonly JwtSettings _jwtSettings;
    private readonly JwtAuthenticationService _authService;
    private readonly Mock<ILogger<JwtAuthenticationService>> _loggerMock;

    public SessionIntegrationTests()
    {
        _sessionRepo = new InMemoryUserSessionRepository();
        _appointmentRepo = new InMemoryAppointmentRepository();
        _patientRepo = new InMemoryPatientRepository();
        _doctorRepo = new InMemoryDoctorRepository();
        _userRepo = new InMemoryUserRepository();
        _paymentRepo = new InMemoryPaymentRepository();
        _auditLogRepo = new InMemoryAuditLogRepository();
        _unitOfWork = new InMemoryUnitOfWork(
            _appointmentRepo,
            _patientRepo,
            _doctorRepo,
            _userRepo,
            _paymentRepo,
            _auditLogRepo,
            _sessionRepo);
        _passwordHasher = new BcryptPasswordHasher();
        _breachedPasswordCheckerMock = new Mock<IBreachedPasswordChecker>();
        _breachedPasswordCheckerMock
            .Setup(x => x.IsPasswordBreachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _jwtSettings = new JwtSettings
        {
            Secret = "ThisIsATestSecretKeyThatIsAtLeast32Characters!",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpirationInMinutes = 60,
            RefreshTokenExpirationInDays = 7
        };

        _loggerMock = new Mock<ILogger<JwtAuthenticationService>>();

        _authService = new JwtAuthenticationService(
            _unitOfWork,
            _passwordHasher,
            _breachedPasswordCheckerMock.Object,
            _jwtSettings,
            _loggerMock.Object,
            redis: null);
    }

    [Fact]
    public async Task RevokeFamily_BlocksRefresh()
    {
        var user = await SeedUserAsync();
        var loginResult = await _authService.LoginAsync("testuser", "password");
        var session = new UserSession(user.Id, loginResult.Value.FamilyId, "Agent", "127.0.0.1");
        await _unitOfWork.UserSessions.AddAsync(session);
        await _unitOfWork.SaveChangesAsync();

        await _authService.RevokeFamilyAsync(loginResult.Value.FamilyId);

        var refreshResult = await _authService.RefreshTokenAsync(loginResult.Value.RefreshToken);
        refreshResult.IsFailure.Should().BeTrue();
        refreshResult.Error.Should().Contain("Session has been revoked");
    }

    [Fact]
    public async Task TokenReuse_RevokesFamily_BlocksNewToken()
    {
        var user = await SeedUserAsync();
        var loginResult = await _authService.LoginAsync("testuser", "password");
        var session = new UserSession(user.Id, loginResult.Value.FamilyId, "Agent", "127.0.0.1");
        await _unitOfWork.UserSessions.AddAsync(session);
        await _unitOfWork.SaveChangesAsync();

        var refresh1 = await _authService.RefreshTokenAsync(loginResult.Value.RefreshToken);
        refresh1.IsSuccess.Should().BeTrue();

        var reuseOld = await _authService.RefreshTokenAsync(loginResult.Value.RefreshToken);
        reuseOld.IsFailure.Should().BeTrue();

        var reuseNew = await _authService.RefreshTokenAsync(refresh1.Value.RefreshToken);
        reuseNew.IsFailure.Should().BeTrue("family should be revoked after reuse");
    }

    [Fact]
    public async Task RevokeAllUserSessions_RevokesAllFamilies()
    {
        var user = await SeedUserAsync();
        var login1 = await _authService.LoginAsync("testuser", "password");
        var session1 = new UserSession(user.Id, login1.Value.FamilyId, "Agent1", "10.0.0.1");
        await _unitOfWork.UserSessions.AddAsync(session1);

        var login2 = await _authService.LoginAsync("testuser", "password");
        var session2 = new UserSession(user.Id, login2.Value.FamilyId, "Agent2", "10.0.0.2");
        await _unitOfWork.UserSessions.AddAsync(session2);
        await _unitOfWork.SaveChangesAsync();

        await _authService.RevokeAllUserSessionsAsync(user.Id);

        (await _unitOfWork.UserSessions.GetActiveByUserIdAsync(user.Id)).Should().BeEmpty();
        session1.IsRevoked.Should().BeTrue();
        session2.IsRevoked.Should().BeTrue();
    }

    private async Task<User> SeedUserAsync()
    {
        var email = Email.Create("test@example.com");
        var passwordHash = _passwordHasher.HashPassword("password");
        var user = User.Create("testuser", email, passwordHash, UserRole.Patient);
        await _userRepo.AddAsync(user);
        return user;
    }
}
