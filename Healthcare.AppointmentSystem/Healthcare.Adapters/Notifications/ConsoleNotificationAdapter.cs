using Healthcare.Application.Ports.Notifications;
using Healthcare.Domain.Entities;

namespace Healthcare.Adapters.Notifications;

/// <summary>
/// Console-based notification adapter for development and testing.
/// </summary>
/// <remarks>
/// Design Pattern: Adapter Pattern + Strategy Pattern
/// 
/// This is a STRATEGY for sending notifications via console output.
/// 
/// When to use:
/// - Development environment
/// - Unit/Integration testing
/// - Quick debugging
/// 
/// Benefits:
/// - Zero external dependencies
/// - Instant feedback
/// - No configuration required
/// 
/// Production Alternative:
/// Replace with EmailNotificationAdapter for real SMTP emails.
/// </remarks>
public sealed class ConsoleNotificationAdapter : INotificationService
{
    private const string Separator = "═════════════════════════════════════════════════════";

    public Task SendAppointmentConfirmationAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine();
        Console.WriteLine(Separator);
        Console.WriteLine("📧 APPOINTMENT CONFIRMATION");
        Console.WriteLine(Separator);
        Console.ResetColor();

        Console.WriteLine($"To: {appointment.Patient.Email}");
        Console.WriteLine($"Subject: Appointment Confirmed - {appointment.ScheduledTime.ToDisplayString()}");
        Console.WriteLine();
        Console.WriteLine($"Dear {appointment.Patient.FullName},");
        Console.WriteLine();
        Console.WriteLine($"Your appointment has been CONFIRMED:");
        Console.WriteLine($"  Doctor: {appointment.Doctor.FullName}");
        Console.WriteLine($"  Date & Time: {appointment.ScheduledTime.ToDisplayString()}");
        Console.WriteLine($"  Reason: {appointment.Reason}");
        Console.WriteLine($"  Fee: {appointment.ConsultationFee.ToDisplayString()}");
        Console.WriteLine();
        Console.WriteLine("Please arrive 10 minutes early.");
        Console.WriteLine();
        Console.WriteLine(Separator);
        Console.WriteLine();

        return Task.CompletedTask;
    }

    public Task SendAppointmentReminderAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine();
        Console.WriteLine(Separator);
        Console.WriteLine("⏰ APPOINTMENT REMINDER");
        Console.WriteLine(Separator);
        Console.ResetColor();

        Console.WriteLine($"To: {appointment.Patient.Email}");
        Console.WriteLine($"Subject: Reminder - Appointment Tomorrow");
        Console.WriteLine();
        Console.WriteLine($"Dear {appointment.Patient.FullName},");
        Console.WriteLine();
        Console.WriteLine($"This is a reminder of your upcoming appointment:");
        Console.WriteLine($"  Doctor: {appointment.Doctor.FullName}");
        Console.WriteLine($"  Date & Time: {appointment.ScheduledTime.ToDisplayString()}");
        Console.WriteLine($"  Location: Healthcare Clinic");
        Console.WriteLine();
        Console.WriteLine("Please confirm or reschedule if needed.");
        Console.WriteLine();
        Console.WriteLine(Separator);
        Console.WriteLine();

        return Task.CompletedTask;
    }

    public Task SendAppointmentCancellationAsync(
        Appointment appointment,
        CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine();
        Console.WriteLine(Separator);
        Console.WriteLine("❌ APPOINTMENT CANCELLED");
        Console.WriteLine(Separator);
        Console.ResetColor();

        Console.WriteLine($"To: {appointment.Patient.Email}");
        Console.WriteLine($"Subject: Appointment Cancelled");
        Console.WriteLine();
        Console.WriteLine($"Dear {appointment.Patient.FullName},");
        Console.WriteLine();
        Console.WriteLine($"Your appointment has been CANCELLED:");
        Console.WriteLine($"  Doctor: {appointment.Doctor.FullName}");
        Console.WriteLine($"  Scheduled Time: {appointment.ScheduledTime.ToDisplayString()}");
        Console.WriteLine($"  Reason: {appointment.CancellationReason}");
        Console.WriteLine();
        Console.WriteLine("Please book a new appointment if needed.");
        Console.WriteLine();
        Console.WriteLine(Separator);
        Console.WriteLine();

        return Task.CompletedTask;
    }

    public Task SendAppointmentRescheduledAsync(
        Appointment appointment,
        DateTime oldTime,
        CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine();
        Console.WriteLine(Separator);
        Console.WriteLine("🔄 APPOINTMENT RESCHEDULED");
        Console.WriteLine(Separator);
        Console.ResetColor();

        Console.WriteLine($"To: {appointment.Patient.Email}");
        Console.WriteLine($"Subject: Appointment Rescheduled");
        Console.WriteLine();
        Console.WriteLine($"Dear {appointment.Patient.FullName},");
        Console.WriteLine();
        Console.WriteLine($"Your appointment has been RESCHEDULED:");
        Console.WriteLine($"  Doctor: {appointment.Doctor.FullName}");
        Console.WriteLine($"  Old Time: {oldTime:dddd, MMMM dd, yyyy 'at' h:mm tt}");
        Console.WriteLine($"  New Time: {appointment.ScheduledTime.ToDisplayString()}");
        Console.WriteLine();
        Console.WriteLine("Please confirm the new time.");
        Console.WriteLine();
        Console.WriteLine(Separator);
        Console.WriteLine();

        return Task.CompletedTask;
    }
}