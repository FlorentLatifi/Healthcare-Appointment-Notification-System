using Healthcare.Application.Common;
using Healthcare.Application.Ports.Authentication;
using Healthcare.Application.Ports.Repositories;
using Microsoft.Extensions.Logging;

namespace Healthcare.Application.Commands.ResetPassword;

public sealed class ResetPasswordHandler : ICommandHandler<ResetPasswordCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthenticationService _authService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<ResetPasswordHandler> _logger;

    public ResetPasswordHandler(
        IUnitOfWork unitOfWork,
        IAuthenticationService authService,
        IPasswordHasher passwordHasher,
        ILogger<ResetPasswordHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _authService = authService;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByEmailAsync(command.Email, cancellationToken);

        if (user == null || !user.IsActive)
        {
            _logger.LogWarning("Password reset failed: user not found or inactive for email {Email}", command.Email);
            return Result.Failure("Invalid or expired reset token.");
        }

        var validateResult = await _authService.ValidateAndConsumePasswordResetTokenAsync(user.Id, command.Token, cancellationToken);
        if (validateResult.IsFailure)
        {
            _logger.LogWarning("Password reset failed: invalid or expired token for user {UserId}", user.Id);
            return Result.Failure("Invalid or expired reset token.");
        }

        var newPasswordHash = _passwordHasher.HashPassword(command.NewPassword);
        user.SetPasswordHash(newPasswordHash);
        await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _authService.RevokeAllUserSessionsAsync(user.Id, cancellationToken);

        _logger.LogInformation("Password reset successful for user {UserId}", user.Id);
        return Result.Success();
    }
}
