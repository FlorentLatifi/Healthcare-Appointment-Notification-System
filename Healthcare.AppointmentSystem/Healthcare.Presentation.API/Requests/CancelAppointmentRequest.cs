namespace Healthcare.Presentation.API.Requests;

/// <summary>
/// Request model for cancelling an appointment.
/// </summary>
public sealed class CancelAppointmentRequest
{
    /// <summary>
    /// Gets or sets the appointment ID to cancel.
    /// </summary>
    /// <example>5</example>
    public int AppointmentId { get; set; }

    /// <summary>
    /// Gets or sets the reason for cancellation.
    /// </summary>
    /// <example>Patient requested reschedule due to emergency</example>
    public string CancellationReason { get; set; } = string.Empty;
}