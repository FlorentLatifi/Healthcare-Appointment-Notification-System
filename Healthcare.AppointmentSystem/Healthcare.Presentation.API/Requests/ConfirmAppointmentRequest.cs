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

    /// <summary>
    /// Set to true to let a Doctor/Admin confirm this appointment even
    /// though it hasn't been paid yet (e.g. emergency walk-in). Requires
    /// <see cref="OverrideReason"/> to be provided.
    /// </summary>
    /// <example>false</example>
    public bool OverridePaymentRequirement { get; set; }

    /// <summary>
    /// Justification for confirming without payment. Required (min. 10
    /// characters) when <see cref="OverridePaymentRequirement"/> is true.
    /// </summary>
    /// <example>Emergency walk-in; patient will settle payment after treatment.</example>
    public string? OverrideReason { get; set; }
}