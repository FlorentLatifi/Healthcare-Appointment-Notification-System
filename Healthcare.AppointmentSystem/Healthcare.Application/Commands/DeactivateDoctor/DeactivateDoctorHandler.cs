using Healthcare.Application.Common;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Events;

namespace Healthcare.Application.Commands.DeactivateDoctor;

public sealed class DeactivateDoctorHandler : ICommandHandler<DeactivateDoctorCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public DeactivateDoctorHandler(IUnitOfWork unitOfWork, IDomainEventDispatcher eventDispatcher)
    {
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<Result> HandleAsync(
        DeactivateDoctorCommand command,
        CancellationToken cancellationToken = default)
    {
        var doctor = await _unitOfWork.Doctors
            .GetByIdAsync(command.DoctorId, cancellationToken);

        if (doctor is null)
        {
            return Result.Failure($"Doctor with ID '{command.DoctorId}' not found");
        }

        try
        {
            doctor.Deactivate();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }

        await _unitOfWork.Doctors.UpdateAsync(doctor, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _eventDispatcher.DispatchAsync(
            new DoctorCacheInvalidationNeededEvent(), cancellationToken);

        return Result.Success();
    }
}
