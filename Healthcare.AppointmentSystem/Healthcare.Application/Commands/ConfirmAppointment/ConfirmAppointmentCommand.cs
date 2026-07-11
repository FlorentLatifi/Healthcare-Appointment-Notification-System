using Healthcare.Application.Common;
using MediatR;

namespace Healthcare.Application.Commands.ConfirmAppointment;

/// <summary>
/// Command to confirm an appointment (MediatR + legacy ICommand during migration).
/// </summary>
public sealed class ConfirmAppointmentCommand : IRequest<Result>, ICommand<Result>, ITransactionalRequest
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