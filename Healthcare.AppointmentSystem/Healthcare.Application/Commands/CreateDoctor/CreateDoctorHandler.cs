using Healthcare.Application.Common;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.Events;
using Healthcare.Domain.ValueObjects;

namespace Healthcare.Application.Commands.CreateDoctor;

public sealed class CreateDoctorHandler : ICommandHandler<CreateDoctorCommand, Result<int>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public CreateDoctorHandler(IUnitOfWork unitOfWork, IDomainEventDispatcher eventDispatcher)
    {
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<Result<int>> HandleAsync(
        CreateDoctorCommand command,
        CancellationToken cancellationToken = default)
    {
        var existingDoctor = await _unitOfWork.Doctors
            .GetByEmailAsync(command.Email, cancellationToken);

        if (existingDoctor is not null)
        {
            return Result<int>.Failure($"A doctor with email '{command.Email}' already exists");
        }

        // Check user link restrictions early, before creating any entity
        User? requestingUser = null;
        if (command.RequestingUserId.HasValue)
        {
            requestingUser = await _unitOfWork.Users.GetByIdAsync(command.RequestingUserId.Value, cancellationToken);
            if (requestingUser == null)
                return Result<int>.Failure("Authenticated user not found.");

            if (requestingUser.DoctorId.HasValue)
                return Result<int>.Failure("This account is already linked to a doctor profile.");
        }

        Email email;
        PhoneNumber phoneNumber;
        Money consultationFee;
        Specialty specialty;

        try
        {
            email = Email.Create(command.Email);
            phoneNumber = PhoneNumber.Create(command.PhoneNumber);
            consultationFee = Money.Create(
                command.ConsultationFeeAmount,
                command.ConsultationFeeCurrency);

            if (!Enum.TryParse<Specialty>(command.Specialty, true, out specialty))
            {
                return Result<int>.Failure($"Invalid specialty: {command.Specialty}");
            }
        }
        catch (Exception ex)
        {
            return Result<int>.Failure($"Invalid input: {ex.Message}");
        }

        Doctor doctor;
        try
        {
            doctor = Doctor.Create(
                command.FirstName,
                command.LastName,
                email,
                phoneNumber,
                command.LicenseNumber,
                consultationFee,
                command.YearsOfExperience,
                specialty);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure($"Failed to create doctor: {ex.Message}");
        }

        // Persist doctor and link user atomically.
        // Doctor.Id is a SQL identity: INSERT must flush before reading Id for LinkToDoctor.
        // Linking before SaveChanges would persist User.DoctorId = 0 (no navigation/FK cascade).
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _unitOfWork.Doctors.AddAsync(doctor, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken); // materialize Doctor.Id

            if (requestingUser != null)
            {
                requestingUser.LinkToDoctor(doctor.Id);
                await _unitOfWork.Users.UpdateAsync(requestingUser, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        await _eventDispatcher.DispatchAsync(
            new DoctorCacheInvalidationNeededEvent(doctor.Id), cancellationToken);

        return Result<int>.Success(doctor.Id);
    }
}
