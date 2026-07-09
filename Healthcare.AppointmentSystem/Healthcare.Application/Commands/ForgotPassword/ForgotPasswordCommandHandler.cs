using Healthcare.Application.Common;
using Healthcare.Application.Ports.Authentication;
using Healthcare.Application.Ports.Notifications;
using Healthcare.Application.Ports.Repositories;
using Microsoft.Extensions.Logging;

namespace Healthcare.Application.Commands.ForgotPassword;

public sealed class ForgotPasswordHandler : ICommandHandler<ForgotPasswordCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthenticationService _authService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ForgotPasswordHandler> _logger;

    public ForgotPasswordHandler(
        IUnitOfWork unitOfWork,
        IAuthenticationService authService,
        INotificationService notificationService,
        ILogger<ForgotPasswordHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _authService = authService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByEmailAsync(command.Email, cancellationToken);

        if (user == null || !user.IsActive)
        {
            _logger.LogInformation("Password reset requested for non-existent or inactive email: {Email}", command.Email);
            return Result.Success();
        }

        var resetToken = await _authService.GeneratePasswordResetTokenAsync(user.Id, cancellationToken);

        var resetLink = $"{command.ResetLinkBaseUrl.TrimEnd('/')}?email={Uri.EscapeDataString(command.Email)}&token={Uri.EscapeDataString(resetToken)}";

        await _notificationService.SendPasswordResetEmailAsync(command.Email, resetLink, cancellationToken);

        _logger.LogInformation("Password reset email sent to {Email}", command.Email);
        return Result.Success();
    }
}
