using Healthcare.Application.Common;
using Healthcare.Application.Ports.Audit;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Audit;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;

namespace Healthcare.Application.Commands.UpdatePatient;

public sealed class UpdatePatientHandler : ICommandHandler<UpdatePatientCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;

    public UpdatePatientHandler(IUnitOfWork unitOfWork, IAuditLogService auditLogService)
    {
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
    }

    public async Task<Result> HandleAsync(
        UpdatePatientCommand command,
        CancellationToken cancellationToken = default)
    {
        var patient = await _unitOfWork.Patients.GetByIdAsync(command.PatientId, cancellationToken);
        if (patient is null)
            return Result.Failure($"Patient with ID '{command.PatientId}' not found.");

        if (!patient.IsActive || patient.IsAnonymized)
            return Result.Failure("Cannot update an inactive or anonymized patient profile.");

        var existingByEmail = await _unitOfWork.Patients.GetByEmailAsync(command.Email, cancellationToken);
        if (existingByEmail is not null && existingByEmail.Id != patient.Id)
            return Result.Failure($"A patient with email '{command.Email}' already exists.");

        if (!Enum.TryParse<Gender>(command.Gender, true, out var gender))
            return Result.Failure("Gender must be Male, Female, or Other.");

        try
        {
            var email = Email.Create(command.Email);
            var phone = PhoneNumber.Create(command.PhoneNumber);
            var address = Address.Create(
                command.Street, command.City, command.State, command.PostalCode, command.Country);

            patient.UpdatePersonalInformation(command.FirstName, command.LastName);
            patient.UpdateDemographics(command.DateOfBirth, gender);
            patient.UpdateContactInformation(email, phone, address);
        }
        catch (Exception ex)
        {
            await _auditLogService.WriteAsync(
                AuditActions.UpdatePatient,
                "Patient",
                command.PatientId,
                AuditOutcome.Failure,
                details: new { Reason = ex.Message },
                cancellationToken: cancellationToken);
            return Result.Failure(ex.Message);
        }

        await _unitOfWork.Patients.UpdateAsync(patient, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogService.WriteAsync(
            AuditActions.UpdatePatient,
            "Patient",
            command.PatientId,
            AuditOutcome.Success,
            details: new { patient.Email.Value },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
