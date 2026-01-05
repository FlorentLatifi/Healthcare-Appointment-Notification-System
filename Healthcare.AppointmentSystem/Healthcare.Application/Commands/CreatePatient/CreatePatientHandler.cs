using Healthcare.Application.Common;
using Healthcare.Application.Ports.Repositories;
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

    public CreatePatientHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<int>> HandleAsync(
        CreatePatientCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Check if patient already exists
            var existingPatient = await _unitOfWork.Patients
                .GetByEmailAsync(command.Email, cancellationToken);

            if (existingPatient is not null)
            {
                return Result<int>.Failure($"A patient with email '{command.Email}' already exists.");
            }

            // 2. Create value objects
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

            // 3. Create patient entity
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

            // 4. Persist patient
            await _unitOfWork.Patients.AddAsync(patient, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(patient.Id);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure($"An unexpected error occurred: {ex.Message}");
        }
    }
}