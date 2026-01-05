using Healthcare.Application.Common;

namespace Healthcare.Application.Commands.ConfirmAppointment;

/// <summary>
/// Command to confirm an appointment.
/// </summary>
public sealed class ConfirmAppointmentCommand : ICommand<Result>
{
    /// <summary>
    /// Gets or sets the appointment ID to confirm.
    /// </summary>
    public int AppointmentId { get; set; }
}