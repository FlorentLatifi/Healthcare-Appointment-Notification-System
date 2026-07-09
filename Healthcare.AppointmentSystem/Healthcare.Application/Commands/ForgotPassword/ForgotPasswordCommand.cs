using Healthcare.Application.Common;

namespace Healthcare.Application.Commands.ForgotPassword;

public sealed class ForgotPasswordCommand : ICommand<Result>
{
    public string Email { get; set; } = string.Empty;
    public string ResetLinkBaseUrl { get; set; } = string.Empty;
}
