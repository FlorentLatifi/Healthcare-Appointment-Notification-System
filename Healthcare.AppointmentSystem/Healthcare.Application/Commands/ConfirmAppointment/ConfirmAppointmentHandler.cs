using Healthcare.Application.Common;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Repositories;

namespace Healthcare.Application.Commands.ConfirmAppointment;

/// <summary>
/// Handler for ConfirmAppointmentCommand.
/// </summary>
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
        try
        {
            // 1. Fetch appointment
            var appointment = await _unitOfWork.Appointments
                .GetByIdAsync(command.AppointmentId, cancellationToken);

            if (appointment is null)
            {
                return Result.Failure($"Appointment with ID {command.AppointmentId} not found.");
            }

            // 2. Confirm appointment (domain logic validates state transitions)
            try
            {
                appointment.Confirm();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Failed to confirm appointment: {ex.Message}");
            }

            // 3. Persist changes
            await _unitOfWork.Appointments.UpdateAsync(appointment, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 4. Dispatch domain events
            await _eventDispatcher.DispatchAsync(appointment.DomainEvents, cancellationToken);
            appointment.ClearDomainEvents();

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"An unexpected error occurred: {ex.Message}");
        }
    }
}