using Healthcare.Application.Common;
using Healthcare.Application.Ports.Repositories;
using Microsoft.Extensions.Logging;

namespace Healthcare.Application.Commands.AnonymizePatient;

public sealed class AnonymizePatientHandler : ICommandHandler<AnonymizePatientCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AnonymizePatientHandler> _logger;

    public AnonymizePatientHandler(
        IUnitOfWork unitOfWork,
        ILogger<AnonymizePatientHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(
        AnonymizePatientCommand command,
        CancellationToken cancellationToken = default)
    {
        var patient = await _unitOfWork.Patients
            .GetByIdAsync(command.PatientId, cancellationToken);

        if (patient is null)
        {
            return Result.Failure($"Patient with ID '{command.PatientId}' not found");
        }

        try
        {
            patient.Anonymize();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }

        await _unitOfWork.Patients.UpdateAsync(patient, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Patient {PatientId} anonymized successfully", command.PatientId);

        return Result.Success();
    }
}
