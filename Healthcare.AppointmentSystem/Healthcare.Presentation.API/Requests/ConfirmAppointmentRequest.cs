namespace Healthcare.Presentation.API.Requests;

/// <summary>
/// Request model for confirming an appointment.
/// </summary>
public sealed class ConfirmAppointmentRequest
{
    /// <summary>
    /// Gets or sets the appointment ID to confirm.
    /// </summary>
    /// <example>5</example>
    public int AppointmentId { get; set; }
}