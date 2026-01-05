using Healthcare.Domain.Entities;

namespace Healthcare.Application.Ports.Notifications;

/// <summary>
/// Service interface for sending notifications to patients and doctors.
/// </summary>
/// <remarks>
/// Design Pattern: Strategy Pattern + Adapter Pattern
/// 
/// This is a PORT in Hexagonal Architecture. Different ADAPTERS can implement
/// this interface:
/// - EmailNotificationAdapter (sends real emails via SMTP)
/// - SmsNotificationAdapter (sends SMS via Twilio)
/// - ConsoleNotificationAdapter (outputs to console for testing)
/// - CompositeNotificationAdapter (sends via multiple channels)
/// 
/// The application layer doesn't care HOW notifications are sent,
/// only THAT they are sent.
/// </remarks>
public interface INotificationService
{
    /// <summary>
    /// Sends an appointment confirmation notification to patient and doctor.
    /// </summary>
    /// <param name="appointment">The confirmed appointment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAppointmentConfirmationAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an appointment reminder notification (24 hours before).
    /// </summary>
    /// <param name="appointment">The upcoming appointment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAppointmentReminderAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an appointment cancellation notification.
    /// </summary>
    /// <param name="appointment">The cancelled appointment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAppointmentCancellationAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an appointment rescheduled notification.
    /// </summary>
    /// <param name="appointment">The rescheduled appointment.</param>
    /// <param name="oldTime">The previous appointment time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAppointmentRescheduledAsync(
        Appointment appointment,
        DateTime oldTime,
        CancellationToken cancellationToken = default);
}