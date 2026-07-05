using Healthcare.Domain.Common;

namespace Healthcare.Domain.ValueObjects;

public sealed class NotificationPreferences : ValueObject
{
    public bool EmailEnabled { get; }
    public bool SmsEnabled { get; }

    private NotificationPreferences(bool emailEnabled, bool smsEnabled)
    {
        EmailEnabled = emailEnabled;
        SmsEnabled = smsEnabled;
    }

    public static NotificationPreferences Create(bool emailEnabled, bool smsEnabled)
    {
        return new NotificationPreferences(emailEnabled, smsEnabled);
    }

    public static NotificationPreferences Default() => new(emailEnabled: true, smsEnabled: false);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return EmailEnabled;
        yield return SmsEnabled;
    }
}
