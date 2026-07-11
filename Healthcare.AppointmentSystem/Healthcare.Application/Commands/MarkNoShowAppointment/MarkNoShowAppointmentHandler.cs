using Healthcare.Application.Common;
using Healthcare.Application.Observability;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Repositories;

namespace Healthcare.Application.Commands.MarkNoShowAppointment;

public sealed class MarkNoShowAppointmentHandler : ICommandHandler<MarkNoShowAppointmentCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly IBusinessMetrics _metrics;

    public MarkNoShowAppointmentHandler(
        IUnitOfWork unitOfWork,
        IDomainEventDispatcher eventDispatcher,
        IBusinessMetrics metrics)
    {
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
        _metrics = metrics;
    }

    public async Task<Result> HandleAsync(
        MarkNoShowAppointmentCommand command,
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
            appointment.MarkAsNoShow();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to mark appointment as no-show: {ex.Message}");
        }

        await _unitOfWork.Appointments.UpdateAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _eventDispatcher.DispatchAsync(appointment.DomainEvents, cancellationToken);
        appointment.ClearDomainEvents();

        _metrics.AppointmentNoShow();

        return Result.Success();
    }
}
