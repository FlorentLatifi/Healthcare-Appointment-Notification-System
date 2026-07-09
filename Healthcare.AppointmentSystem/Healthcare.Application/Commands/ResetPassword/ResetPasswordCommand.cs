using Healthcare.Application.Common;

namespace Healthcare.Application.Commands.ResetPassword;

public sealed class ResetPasswordCommand : ICommand<Result>
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
