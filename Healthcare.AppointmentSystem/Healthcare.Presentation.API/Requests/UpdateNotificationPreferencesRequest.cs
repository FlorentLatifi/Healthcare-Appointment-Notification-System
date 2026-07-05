namespace Healthcare.Presentation.API.Requests;

public sealed class UpdateNotificationPreferencesRequest
{
    public bool EmailEnabled { get; set; }
    public bool SmsEnabled { get; set; }
}
