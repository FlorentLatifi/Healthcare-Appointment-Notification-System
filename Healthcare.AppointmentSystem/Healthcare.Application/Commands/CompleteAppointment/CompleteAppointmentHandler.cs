using Healthcare.Application.Common;
using Healthcare.Application.Observability;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Repositories;

namespace Healthcare.Application.Commands.CompleteAppointment;

public sealed class CompleteAppointmentHandler : ICommandHandler<CompleteAppointmentCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly IBusinessMetrics _metrics;

    public CompleteAppointmentHandler(
        IUnitOfWork unitOfWork,
        IDomainEventDispatcher eventDispatcher,
        IBusinessMetrics metrics)
    {
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
        _metrics = metrics;
    }

    public async Task<Result> HandleAsync(
        CompleteAppointmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var appointment = await _unitOfWork.Appointments
                .GetByIdAsync(command.AppointmentId, cancellationToken);

        if (appointment is null)
        {
            return Result.Failure($"Appointment with ID {command.AppointmentId} not found.");
        }

        try
        {
            appointment.Complete(command.DoctorNotes);
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to complete appointment: {ex.Message}");
        }

        await _unitOfWork.Appointments.UpdateAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _eventDispatcher.DispatchAsync(appointment.DomainEvents, cancellationToken);
        appointment.ClearDomainEvents();

        _metrics.AppointmentCompleted();

        return Result.Success();
    }
}
