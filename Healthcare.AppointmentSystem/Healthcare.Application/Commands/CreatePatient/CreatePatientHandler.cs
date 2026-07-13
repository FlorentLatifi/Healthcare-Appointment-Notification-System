using Healthcare.Application.Common;
using Healthcare.Application.Ports.Audit;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Audit;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;

namespace Healthcare.Application.Commands.CreatePatient;

/// <summary>
/// Handler for CreatePatientCommand.
/// </summary>
public sealed class CreatePatientHandler : ICommandHandler<CreatePatientCommand, Result<int>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;

    public CreatePatientHandler(IUnitOfWork unitOfWork, IAuditLogService auditLogService)
    {
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
    }

    public async Task<Result<int>> HandleAsync(
        CreatePatientCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Check if patient already exists
        var existingPatient = await _unitOfWork.Patients
            .GetByEmailAsync(command.Email, cancellationToken);

        if (existingPatient is not null)
        {
            await WriteAuditAsync(command, null, AuditOutcome.Failure, "duplicate_email", cancellationToken);
            return Result<int>.Failure($"A patient with email '{command.Email}' already exists.");
        }

        // 2. Check that the requesting user exists and is not already linked to a patient
        var requestingUser = await _unitOfWork.Users.GetByIdAsync(command.RequestingUserId, cancellationToken);
        if (requestingUser == null)
            return Result<int>.Failure("Authenticated user not found.");

        if (requestingUser.PatientId.HasValue)
            return Result<int>.Failure("This account is already linked to a patient profile.");

        // 3. Create value objects
        Email email;
        PhoneNumber phoneNumber;
        Address address;
        Gender gender;

        try
        {
            email = Email.Create(command.Email);
            phoneNumber = PhoneNumber.Create(command.PhoneNumber);
            address = Address.Create(
                command.Street,
                command.City,
                command.State,
                command.PostalCode,
                command.Country);

            gender = Enum.Parse<Gender>(command.Gender, ignoreCase: true);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure($"Invalid input: {ex.Message}");
        }

        // 4. Create patient entity
        Patient patient;
        try
        {
            patient = Patient.Create(
                command.FirstName,
                command.LastName,
                email,
                phoneNumber,
                command.DateOfBirth,
                gender,
                address);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure($"Failed to create patient: {ex.Message}");
        }

        // 5. Persist patient and link user atomically.
        // Patient.Id is a SQL identity: INSERT must flush before reading Id for LinkToPatient.
        // Linking before SaveChanges would persist User.PatientId = 0 (no navigation/FK cascade).
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _unitOfWork.Patients.AddAsync(patient, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken); // materialize Patient.Id

            requestingUser.LinkToPatient(patient.Id);
            await _unitOfWork.Users.UpdateAsync(requestingUser, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        await WriteAuditAsync(command, patient.Id, AuditOutcome.Success, null, cancellationToken);
        return Result<int>.Success(patient.Id);
    }

    private Task WriteAuditAsync(
        CreatePatientCommand command,
        int? patientId,
        AuditOutcome outcome,
        string? error,
        CancellationToken cancellationToken)
        => _auditLogService.WriteAsync(
            AuditActions.CreatePatient,
            "Patient",
            patientId,
            outcome,
            details: new
            {
                command.RequestingUserId,
                // No email / DOB / address in audit details (PHI minimization)
                error
            },
            actorUserIdOverride: command.RequestingUserId,
            cancellationToken: cancellationToken);
}