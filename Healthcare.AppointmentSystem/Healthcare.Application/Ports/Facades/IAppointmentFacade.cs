using Healthcare.Application.Common;
using Healthcare.Application.DTOs;

namespace Healthcare.Application.Ports.Facades;

/// <summary>
/// Facade interface for appointment operations.
/// </summary>
/// <remarks>
/// Design Pattern: Facade (Structural)
/// 
/// WHY Facade?
///   Booking an appointment involves multiple subsystems:
///   - Command building (Builder Pattern)
///   - Business logic (Command Handler)
///   - Pricing (Strategy Pattern)
///   - Notifications (Adapter Pattern)
///   - Event dispatching (Observer Pattern)
/// 
///   The Facade hides ALL of this behind simple methods.
///   Controllers stay thin and focused on HTTP concerns only.
/// 
/// WHERE (Hexagonal Architecture):
///   Interface (PORT) lives in Application layer.
///   Implementation (ADAPTER) also in Application layer
///   since it orchestrates application-level concerns.
/// 
/// DIFFERENCE from Application Service:
///   Application Service handles ONE use case.
///   Facade coordinates MULTIPLE use cases into
///   higher-level operations.
/// </remarks>
public interface IAppointmentFacade
{
    /// <summary>
    /// Books a new appointment with automatic pricing and notification.
    /// </summary>
    Task<Result<AppointmentDto>> BookAppointmentAsync(
        int patientId,
        int doctorId,
        DateTime scheduledTime,
        string reason,
        string appointmentType = "Standard",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms an appointment and notifies the patient.
    /// </summary>
    Task<Result> ConfirmAppointmentAsync(
        int appointmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an appointment and notifies the patient.
    /// </summary>
    Task<Result> CancelAppointmentAsync(
        int appointmentId,
        string reason,
        CancellationToken cancellationToken = default);
}
