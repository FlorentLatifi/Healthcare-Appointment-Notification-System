namespace Healthcare.Presentation.API.Requests;

public sealed class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}
