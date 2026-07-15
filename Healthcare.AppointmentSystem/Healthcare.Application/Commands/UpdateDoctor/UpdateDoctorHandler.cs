using Healthcare.Application.Common;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Enums;
using Healthcare.Domain.Events;
using Healthcare.Domain.ValueObjects;

namespace Healthcare.Application.Commands.UpdateDoctor;

public sealed class UpdateDoctorHandler : ICommandHandler<UpdateDoctorCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public UpdateDoctorHandler(IUnitOfWork unitOfWork, IDomainEventDispatcher eventDispatcher)
    {
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<Result> HandleAsync(
        UpdateDoctorCommand command,
        CancellationToken cancellationToken = default)
    {
        var doctor = await _unitOfWork.Doctors.GetByIdAsync(command.DoctorId, cancellationToken);
        if (doctor is null)
            return Result.Failure($"Doctor with ID '{command.DoctorId}' not found.");

        if (!doctor.IsActive)
            return Result.Failure("Cannot update an inactive doctor profile.");

        var existingByEmail = await _unitOfWork.Doctors.GetByEmailAsync(command.Email, cancellationToken);
        if (existingByEmail is not null && existingByEmail.Id != doctor.Id)
            return Result.Failure($"A doctor with email '{command.Email}' already exists.");

        if (!Enum.TryParse<Specialty>(command.Specialty, true, out var specialty))
            return Result.Failure($"Unknown specialty '{command.Specialty}'.");

        try
        {
            var email = Email.Create(command.Email);
            var phone = PhoneNumber.Create(command.PhoneNumber);
            var fee = Money.Create(command.ConsultationFeeAmount, command.ConsultationFeeCurrency);

            doctor.UpdatePersonalInformation(command.FirstName, command.LastName);
            doctor.UpdateContactInformation(email, phone);
            doctor.UpdateLicenseNumber(command.LicenseNumber);
            doctor.UpdateYearsOfExperience(command.YearsOfExperience);
            doctor.ReplacePrimarySpecialty(specialty);

            // Fee update is constrained by domain (max 50% drop per change).
            if (doctor.ConsultationFee.Amount != fee.Amount
                || !string.Equals(doctor.ConsultationFee.Currency, fee.Currency, StringComparison.OrdinalIgnoreCase))
            {
                doctor.UpdateConsultationFee(fee);
            }
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }

        await _unitOfWork.Doctors.UpdateAsync(doctor, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _eventDispatcher.DispatchAsync(
            new DoctorCacheInvalidationNeededEvent(doctor.Id), cancellationToken);

        return Result.Success();
    }
}
