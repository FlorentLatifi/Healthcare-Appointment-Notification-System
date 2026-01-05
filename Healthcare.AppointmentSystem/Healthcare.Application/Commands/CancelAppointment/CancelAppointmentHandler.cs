using Healthcare.Application.Common;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Repositories;

namespace Healthcare.Application.Commands.CancelAppointment;

/// <summary>
/// Handler for CancelAppointmentCommand.
/// </summary>
public sealed class CancelAppointmentHandler : ICommandHandler<CancelAppointmentCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public CancelAppointmentHandler(
        IUnitOfWork unitOfWork,
        IDomainEventDispatcher eventDispatcher)
    {
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<Result> HandleAsync(
        CancelAppointmentCommand command,
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

            // 2. Cancel appointment (domain logic validates)
            try
            {
                appointment.Cancel(command.CancellationReason);
            }
            catch (Exception ex)
            {
                return Result.Failure($"Failed to cancel appointment: {ex.Message}");
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