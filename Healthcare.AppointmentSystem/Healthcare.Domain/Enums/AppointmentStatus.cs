namespace Healthcare.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of an appointment.
/// </summary>
/// <remarks>
/// This enum follows the State pattern conceptually - different statuses allow different operations.
/// Status transitions are enforced in the Appointment entity's business logic.
/// 
/// Valid Transitions:
/// Pending → Confirmed → Completed
/// Pending → Cancelled
/// Confirmed → Cancelled
/// Confirmed → NoShow
/// </remarks>
public enum AppointmentStatus
{
    /// <summary>
    /// Appointment has been created but not yet confirmed by the doctor.
    /// Patient can cancel, doctor can confirm or cancel.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Appointment has been confirmed by the doctor.
    /// Patient and doctor can cancel, appointment can be marked as completed or no-show.
    /// </summary>
    Confirmed = 2,

    /// <summary>
    /// Appointment has been completed successfully.
    /// This is a terminal state - no further transitions allowed.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Appointment was cancelled by patient or doctor.
    /// This is a terminal state - no further transitions allowed.
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// Patient did not show up for a confirmed appointment.
    /// This is a terminal state - no further transitions allowed.
    /// </summary>
    NoShow = 5
}