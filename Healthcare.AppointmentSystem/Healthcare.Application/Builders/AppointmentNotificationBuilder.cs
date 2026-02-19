namespace Healthcare.Application.Builders;

/// <summary>
/// Represents a constructed notification message.
/// </summary>
public sealed class AppointmentNotification
{
    public string RecipientEmail { get; init; } = string.Empty;
    public string RecipientName { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;
    public DateTime ScheduledAt { get; init; }
}

/// <summary>
/// Builder for constructing appointment notification messages.
/// </summary>
/// <remarks>
/// Design Pattern: Builder (Creational)
/// 
/// WHY a second Builder?
///   Notification messages have:
///   - Variable recipients
///   - Variable channels (email, SMS, console)
///   - Variable content based on event type
/// 
///   Builder makes construction readable and safe.
/// 
/// USAGE:
///   var notification = new AppointmentNotificationBuilder()
///       .To("john@example.com", "John Doe")
///       .WithSubject("Appointment Confirmed")
///       .WithBody("Your appointment is confirmed for...")
///       .ViaEmail()
///       .ScheduledFor(DateTime.UtcNow)
///       .Build();
/// </remarks>
public sealed class AppointmentNotificationBuilder
{
    private string _email = string.Empty;
    private string _name = string.Empty;
    private string _subject = string.Empty;
    private string _body = string.Empty;
    private string _channel = "Console";
    private DateTime _scheduledAt = DateTime.UtcNow;

    // ── SETTERS ──────────────────────────────────────────

    public AppointmentNotificationBuilder To(
        string email, string name)
    {
        _email = email;
        _name = name;
        return this;
    }

    public AppointmentNotificationBuilder WithSubject(string subject)
    {
        _subject = subject;
        return this;
    }

    public AppointmentNotificationBuilder WithBody(string body)
    {
        _body = body;
        return this;
    }

    public AppointmentNotificationBuilder ViaEmail()
    {
        _channel = "Email";
        return this;
    }

    public AppointmentNotificationBuilder ViaSms()
    {
        _channel = "SMS";
        return this;
    }

    public AppointmentNotificationBuilder ViaConsole()
    {
        _channel = "Console";
        return this;
    }

    public AppointmentNotificationBuilder ScheduledFor(DateTime scheduledAt)
    {
        _scheduledAt = scheduledAt;
        return this;
    }

    // ── BUILD ────────────────────────────────────────────

    public AppointmentNotification Build()
    {
        if (string.IsNullOrWhiteSpace(_email))
            throw new InvalidOperationException(
                "Recipient email is required. Call To() first.");

        if (string.IsNullOrWhiteSpace(_subject))
            throw new InvalidOperationException(
                "Subject is required. Call WithSubject() first.");

        if (string.IsNullOrWhiteSpace(_body))
            throw new InvalidOperationException(
                "Body is required. Call WithBody() first.");

        return new AppointmentNotification
        {
            RecipientEmail = _email,
            RecipientName = _name,
            Subject = _subject,
            Body = _body,
            Channel = _channel,
            ScheduledAt = _scheduledAt
        };
    }
}