using Healthcare.Application.Common;

namespace Healthcare.Application.Commands.CancelAppointment;

/// <summary>
/// Command to cancel an appointment.
/// </summary>
public sealed class CancelAppointmentCommand : ICommand<Result>
{
    /// <summary>
    /// Gets or sets the appointment ID to cancel.
    /// </summary>
    public int AppointmentId { get; set; }

    /// <summary>
    /// Gets or sets the reason for cancellation.
    /// </summary>
    public string CancellationReason { get; set; } = string.Empty;
}