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

    /// <summary>
    /// Gets or sets a value indicating whether to override the payment requirement.
    /// </summary>
    public bool OverridePaymentRequirement { get; set; }

    /// <summary>
    /// Gets or sets the reason for overriding the payment requirement.
    /// </summary>
    public string? OverrideReason { get; set; }
}