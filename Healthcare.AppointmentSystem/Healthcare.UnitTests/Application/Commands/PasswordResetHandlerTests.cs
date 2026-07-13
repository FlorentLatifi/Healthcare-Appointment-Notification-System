using FluentAssertions;
using Healthcare.Application.Commands.ForgotPassword;
using Healthcare.Application.Commands.ResetPassword;
using Healthcare.Application.Common;
using Healthcare.Application.Ports.Authentication;
using Healthcare.Application.Ports.Notifications;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace Healthcare.UnitTests.Application.Commands;

public sealed class PasswordResetHandlerTests
{
    private static User MakeUser(int id, string email = "user@example.com")
    {
        var user = User.Create("testuser", Email.Create(email), "old-hash", UserRole.Patient);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, id);
        return user;
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_ReturnsSuccess_WithoutSendingEmail()
    {
        var uow = new Mock<IUnitOfWork>();
        var users = new Mock<IUserRepository>();
        uow.Setup(u => u.Users).Returns(users.Object);
        users.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var auth = new Mock<IAuthenticationService>();
        var notify = new Mock<INotificationService>();

        var handler = new ForgotPasswordHandler(
            uow.Object, auth.Object, notify.Object, Mock.Of<ILogger<ForgotPasswordHandler>>());

        var result = await handler.HandleAsync(new ForgotPasswordCommand
        {
            Email = "missing@example.com",
            ResetLinkBaseUrl = "http://localhost:5173/reset-password"
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        auth.Verify(a => a.GeneratePasswordResetTokenAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        notify.Verify(n => n.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPassword_KnownUser_GeneratesTokenAndSendsEmail()
    {
        var user = MakeUser(5);
        var uow = new Mock<IUnitOfWork>();
        var users = new Mock<IUserRepository>();
        uow.Setup(u => u.Users).Returns(users.Object);
        users.Setup(r => r.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.GeneratePasswordResetTokenAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync("raw-token-value");

        var notify = new Mock<INotificationService>();
        string? capturedLink = null;
        notify.Setup(n => n.SendPasswordResetEmailAsync(
                "user@example.com", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, link, _) => capturedLink = link)
            .Returns(Task.CompletedTask);

        var handler = new ForgotPasswordHandler(
            uow.Object, auth.Object, notify.Object, Mock.Of<ILogger<ForgotPasswordHandler>>());

        var result = await handler.HandleAsync(new ForgotPasswordCommand
        {
            Email = "user@example.com",
            ResetLinkBaseUrl = "http://localhost:5173/reset-password"
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        capturedLink.Should().Contain("token=raw-token-value");
        capturedLink.Should().Contain("email=user%40example.com");
        notify.Verify(n => n.SendPasswordResetEmailAsync(
            "user@example.com", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_ReturnsGenericFailure_DoesNotChangePassword()
    {
        var user = MakeUser(5);
        var uow = new Mock<IUnitOfWork>();
        var users = new Mock<IUserRepository>();
        uow.Setup(u => u.Users).Returns(users.Object);
        users.Setup(r => r.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.ValidateAndConsumePasswordResetTokenAsync(5, "bad", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("Invalid or expired reset token."));

        var hasher = new Mock<IPasswordHasher>();
        var breach = new Mock<IBreachedPasswordChecker>();
        breach.Setup(b => b.IsPasswordBreachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new ResetPasswordHandler(
            uow.Object, auth.Object, hasher.Object, breach.Object, Mock.Of<ILogger<ResetPasswordHandler>>());

        var result = await handler.HandleAsync(new ResetPasswordCommand
        {
            Email = "user@example.com",
            Token = "bad",
            NewPassword = "Str0ng!Passw0rd"
        }, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Invalid or expired reset token.");
        hasher.Verify(h => h.HashPassword(It.IsAny<string>()), Times.Never);
        users.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_UpdatesHash_RevokesSessions()
    {
        var user = MakeUser(5);
        var uow = new Mock<IUnitOfWork>();
        var users = new Mock<IUserRepository>();
        uow.Setup(u => u.Users).Returns(users.Object);
        users.Setup(r => r.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        users.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.ValidateAndConsumePasswordResetTokenAsync(5, "good-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        auth.Setup(a => a.RevokeAllUserSessionsAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.HashPassword("Str0ng!Passw0rd")).Returns("new-hash");

        var breach = new Mock<IBreachedPasswordChecker>();
        breach.Setup(b => b.IsPasswordBreachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new ResetPasswordHandler(
            uow.Object, auth.Object, hasher.Object, breach.Object, Mock.Of<ILogger<ResetPasswordHandler>>());

        var result = await handler.HandleAsync(new ResetPasswordCommand
        {
            Email = "user@example.com",
            Token = "good-token",
            NewPassword = "Str0ng!Passw0rd"
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be("new-hash");
        auth.Verify(a => a.RevokeAllUserSessionsAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_BreachedPassword_DoesNotConsumeToken()
    {
        var user = MakeUser(5);
        var uow = new Mock<IUnitOfWork>();
        var users = new Mock<IUserRepository>();
        uow.Setup(u => u.Users).Returns(users.Object);
        users.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var auth = new Mock<IAuthenticationService>();
        var hasher = new Mock<IPasswordHasher>();
        var breach = new Mock<IBreachedPasswordChecker>();
        breach.Setup(b => b.IsPasswordBreachedAsync("Password123!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new ResetPasswordHandler(
            uow.Object, auth.Object, hasher.Object, breach.Object, Mock.Of<ILogger<ResetPasswordHandler>>());

        var result = await handler.HandleAsync(new ResetPasswordCommand
        {
            Email = "user@example.com",
            Token = "good-token",
            NewPassword = "Password123!"
        }, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("breach");
        auth.Verify(a => a.ValidateAndConsumePasswordResetTokenAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
