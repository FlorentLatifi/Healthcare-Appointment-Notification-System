using Healthcare.Application.Ports.Notifications;
using Healthcare.Domain.Entities;
using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Logging;

namespace Healthcare.Adapters.Notifications;

/// <summary>
/// Email notification adapter using SMTP.
/// </summary>
/// <remarks>
/// Design Pattern: Adapter Pattern + Strategy Pattern
/// 
/// This adapter:
/// - Sends REAL emails via SMTP (MailKit library)
/// - Implements professional HTML formatting
/// - Handles errors gracefully (logs but doesn't crash)
/// - Is configured via dependency injection
/// 
/// Configuration Required:
/// - SMTP host, port, username, password
/// - From email address
/// 
/// Production Notes:
/// - Use services like SendGrid, AWS SES, or Mailgun for reliability
/// - Implement retry logic for transient failures
/// - Queue emails for background processing
/// </remarks>
public sealed class EmailNotificationAdapter : INotificationService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailNotificationAdapter> _logger;

    public EmailNotificationAdapter(
        EmailSettings settings,
        ILogger<EmailNotificationAdapter> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task SendAppointmentConfirmationAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Appointment Confirmed - {appointment.ScheduledTime.GetDate()}";

        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <div style='background-color: #4CAF50; color: white; padding: 20px; text-align: center;'>
                    <h1>✅ Appointment Confirmed</h1>
                </div>
                <div style='padding: 20px;'>
                    <p>Dear {appointment.Patient.FullName},</p>
                    
                    <p>Your appointment has been <strong>confirmed</strong>:</p>
                    
                    <table style='border-collapse: collapse; width: 100%; margin: 20px 0;'>
                        <tr style='background-color: #f2f2f2;'>
                            <td style='padding: 10px; border: 1px solid #ddd;'><strong>Doctor</strong></td>
                            <td style='padding: 10px; border: 1px solid #ddd;'>{appointment.Doctor.FullName}</td>
                        </tr>
                        <tr>
                            <td style='padding: 10px; border: 1px solid #ddd;'><strong>Date & Time</strong></td>
                            <td style='padding: 10px; border: 1px solid #ddd;'>{appointment.ScheduledTime.ToDisplayString()}</td>
                        </tr>
                        <tr style='background-color: #f2f2f2;'>
                            <td style='padding: 10px; border: 1px solid #ddd;'><strong>Reason</strong></td>
                            <td style='padding: 10px; border: 1px solid #ddd;'>{appointment.Reason}</td>
                        </tr>
                        <tr>
                            <td style='padding: 10px; border: 1px solid #ddd;'><strong>Consultation Fee</strong></td>
                            <td style='padding: 10px; border: 1px solid #ddd;'>{appointment.ConsultationFee.ToDisplayString()}</td>
                        </tr>
                    </table>
                    
                    <p><strong>Please arrive 10 minutes before your scheduled time.</strong></p>
                    
                    <p>If you need to reschedule or cancel, please contact us as soon as possible.</p>
                    
                    <p>Best regards,<br/>Healthcare Clinic</p>
                </div>
            </body>
            </html>";

        await SendEmailAsync(appointment.Patient.Email, subject, body, cancellationToken);
    }

    public async Task SendAppointmentReminderAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Reminder: Appointment Tomorrow with {appointment.Doctor.FullName}";

        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <div style='background-color: #FFC107; color: black; padding: 20px; text-align: center;'>
                    <h1>⏰ Appointment Reminder</h1>
                </div>
                <div style='padding: 20px;'>
                    <p>Dear {appointment.Patient.FullName},</p>
                    
                    <p>This is a friendly reminder about your upcoming appointment:</p>
                    
                    <table style='border-collapse: collapse; width: 100%; margin: 20px 0;'>
                        <tr style='background-color: #fff3cd;'>
                            <td style='padding: 10px; border: 1px solid #ddd;'><strong>Doctor</strong></td>
                            <td style='padding: 10px; border: 1px solid #ddd;'>{appointment.Doctor.FullName}</td>
                        </tr>
                        <tr>
                            <td style='padding: 10px; border: 1px solid #ddd;'><strong>Date & Time</strong></td>
                            <td style='padding: 10px; border: 1px solid #ddd;'>{appointment.ScheduledTime.ToDisplayString()}</td>
                        </tr>
                        <tr style='background-color: #fff3cd;'>
                            <td style='padding: 10px; border: 1px solid #ddd;'><strong>Location</strong></td>
                            <td style='padding: 10px; border: 1px solid #ddd;'>Healthcare Clinic, Main Building</td>
                        </tr>
                    </table>
                    
                    <p>Please confirm your attendance or reschedule if necessary.</p>
                    
                    <p>Best regards,<br/>Healthcare Clinic</p>
                </div>
            </body>
            </html>";

        await SendEmailAsync(appointment.Patient.Email, subject, body, cancellationToken);
    }

    public async Task SendAppointmentCancellationAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Appointment Cancelled - {appointment.ScheduledTime.GetDate()}";

        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <div style='background-color: #f44336; color: white; padding: 20px; text-align: center;'>
                    <h1>❌ Appointment Cancelled</h1>
                </div>
                <div style='padding: 20px;'>
                    <p>Dear {appointment.Patient.FullName},</p>
                    
                    <p>Your appointment has been <strong>cancelled</strong>:</p>
                    
                    <table style='border-collapse: collapse; width: 100%; margin: 20px 0;'>
                        <tr style='background-color: #ffebee;'>
                            <td style='padding: 10px; border: 1px solid #ddd;'><strong>Doctor</strong></td>
                            <td style='padding: 10px; border: 1px solid #ddd;'>{appointment.Doctor.FullName}</td>
                        </tr>
                        <tr>
                            <td style='padding: 10px; border: 1px solid #ddd;'><strong>Scheduled Time</strong></td>
                            <td style='padding: 10px; border: 1px solid #ddd;'>{appointment.ScheduledTime.ToDisplayString()}</td>
                        </tr>
                        <tr style='background-color: #ffebee;'>
                            <td style='padding: 10px; border: 1px solid #ddd;'><strong>Cancellation Reason</strong></td>
                            <td style='padding: 10px; border: 1px solid #ddd;'>{appointment.CancellationReason}</td>
                        </tr>
                    </table>
                    
                    <p>If you would like to reschedule, please contact us.</p>
                    
                    <p>Best regards,<br/>Healthcare Clinic</p>
                </div>
            </body>
            </html>";

        await SendEmailAsync(appointment.Patient.Email, subject, body, cancellationToken);
    }

    public async Task SendAppointmentRescheduledAsync(
        Appointment appointment,
        DateTime oldTime,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Appointment Rescheduled - New Time: {appointment.ScheduledTime.GetDate()}";

        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <div style='background-color: #2196F3; color: white; padding: 20px; text-align: center;'>
                    <h1>🔄 Appointment Rescheduled</h1>
                </div>
                <div style='padding: 20px;'>
                    <p>Dear {appointment.Patient.FullName},</p>
                    
                    <p>Your appointment has been <strong>rescheduled</strong>:</p>
                    
                    <table style='border-collapse: collapse; width: 100%; margin: 20px 0;'>
                        <tr style='background-color: #e3f2fd;'>
                            <td style='padding: 10px; border: 1px solid #ddd;'><strong>Doctor</strong></td>
                            <td style='padding: 10px; border: 1px solid #ddd;'>{appointment.Doctor.FullName}</td>
                        </tr>
                        <tr>
                            <td style='padding: 10px; border: 1px solid #ddd;'><strong>Old Time</strong></td>
                            <td style='padding: 10px; border: 1px solid #ddd; text-decoration: line-through;'>{oldTime:dddd, MMMM dd, yyyy 'at' h:mm tt}</td>
                        </tr>
                        <tr style='background-color: #e3f2fd;'>
                            <td style='padding: 10px; border: 1px solid #ddd;'><strong>New Time</strong></td>
                            <td style='padding: 10px; border: 1px solid #ddd;'><strong>{appointment.ScheduledTime.ToDisplayString()}</strong></td>
                        </tr>
                    </table>
                    
                    <p>Please confirm the new appointment time.</p>
                    
                    <p>Best regards,<br/>Healthcare Clinic</p>
                </div>
            </body>
            </html>";

        await SendEmailAsync(appointment.Patient.Email, subject, body, cancellationToken);
    }

    /// <summary>
    /// Core method to send email via SMTP.
    /// </summary>
    private async Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, false, cancellationToken);
            await client.AuthenticateAsync(_settings.SmtpUsername, _settings.SmtpPassword, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email sent successfully to {Email}: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}: {Subject}", toEmail, subject);
            // Don't throw - notification failure shouldn't break the business flow
        }
    }
}

/// <summary>
/// Email configuration settings.
/// </summary>
public sealed class EmailSettings
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Healthcare Clinic";
}