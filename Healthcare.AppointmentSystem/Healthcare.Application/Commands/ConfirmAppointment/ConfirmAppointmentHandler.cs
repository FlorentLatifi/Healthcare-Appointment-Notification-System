using Healthcare.Application.Common;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Enums;

namespace Healthcare.Application.Commands.ConfirmAppointment;

/// <summary>
/// Handler for ConfirmAppointmentCommand.
/// </summary>
/// <remarks>
/// Business rule: an appointment cannot move Pending → Confirmed unless a
/// payment for it has already succeeded, UNLESS a Doctor/Admin explicitly
/// overrides that requirement with a documented reason (audited via
/// <see cref="Healthcare.Domain.Events.AppointmentConfirmedEvent.PaymentOverrideReason"/>).
///
/// The rule is only evaluated when the appointment is currently Pending —
/// for any other status, the domain's own state-transition rule in
/// <c>Appointment.Confirm()</c> is what should reject the request, so the
/// payment check is skipped in that case to keep error messages accurate.
/// </remarks>
public sealed class ConfirmAppointmentHandler : ICommandHandler<ConfirmAppointmentCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public ConfirmAppointmentHandler(
        IUnitOfWork unitOfWork,
        IDomainEventDispatcher eventDispatcher)
    {
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<Result> HandleAsync(
        ConfirmAppointmentCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Fetch appointment
        var appointment = await _unitOfWork.Appointments
            .GetByIdAsync(command.AppointmentId, cancellationToken);

        if (appointment is null)
        {
            return Result.Failure($"Appointment with ID {command.AppointmentId} not found.");
        }

        string? overrideReason = null;

        // 2. Payment-before-confirmation business rule. Only relevant for
        //    the Pending -> Confirmed transition; for any other current
        //    status we let appointment.Confirm() below reject it with its
        //    own (more accurate) state-transition error.
        if (appointment.Status == AppointmentStatus.Pending)
        {
            var payment = await _unitOfWork.Payments
                .GetByAppointmentIdAsync(command.AppointmentId, cancellationToken);

            var isPaid = payment is not null && payment.Status == PaymentStatus.Succeeded;

            if (!isPaid)
            {
                if (!command.OverridePaymentRequirement)
                {
                    return Result.Failure(
                        "Appointment cannot be confirmed until payment is completed. " +
                        "A Doctor or Admin may confirm without payment by explicitly " +
                        "overriding this requirement with a reason.");
                }

                if (string.IsNullOrWhiteSpace(command.OverrideReason) ||
                    command.OverrideReason.Trim().Length < 10)
                {
                    return Result.Failure(
                        "Overriding the payment requirement requires a reason of at least 10 characters.");
                }

                overrideReason = command.OverrideReason.Trim();
            }
        }

        // 3. Confirm appointment (domain logic validates state transitions)
        try
        {
            appointment.Confirm(overrideReason);
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to confirm appointment: {ex.Message}");
        }

        // 4. Persist changes
        await _unitOfWork.Appointments.UpdateAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 5. Dispatch domain events
        await _eventDispatcher.DispatchAsync(appointment.DomainEvents, cancellationToken);
        appointment.ClearDomainEvents();

        return Result.Success();
    }
}