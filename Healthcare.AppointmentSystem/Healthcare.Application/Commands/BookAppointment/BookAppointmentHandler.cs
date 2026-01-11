using Healthcare.Application.Common;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Locking;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.ValueObjects;

namespace Healthcare.Application.Commands.BookAppointment;

/// <summary>
/// Handler for BookAppointmentCommand.
/// </summary>
/// <remarks>
/// This handler orchestrates the booking of an appointment:
/// 1. Validates that patient and doctor exist
/// 2. Creates domain entity (which validates business rules)
/// 3. Persists the appointment
/// 4. Dispatches domain events
/// 
/// Design Pattern: Command Handler + Unit of Work + Repository
/// </remarks>
public sealed class BookAppointmentHandler : ICommandHandler<BookAppointmentCommand, Result<int>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly IDistributedLockService _lockService;

    public BookAppointmentHandler(
        IUnitOfWork unitOfWork,
        IDomainEventDispatcher eventDispatcher,
        IDistributedLockService lockService)
    {
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
        _lockService = lockService;
    }

    public async Task<Result<int>> HandleAsync(
        BookAppointmentCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Fetch patient
            var patient = await _unitOfWork.Patients.GetByIdAsync(command.PatientId, cancellationToken);
            if (patient is null)
            {
                return Result<int>.Failure($"Patient with ID {command.PatientId} not found.");
            }

            // 2. Fetch doctor
            var doctor = await _unitOfWork.Doctors.GetByIdAsync(command.DoctorId, cancellationToken);
            if (doctor is null)
            {
                return Result<int>.Failure($"Doctor with ID {command.DoctorId} not found.");
            }

            // 3.1 Create appointment time value object (validates business rules)
            AppointmentTime scheduledTime;
            try
            {
                scheduledTime = AppointmentTime.Create(command.ScheduledTime);
            }
            catch (Exception ex)
            {
                return Result<int>.Failure($"Invalid appointment time: {ex.Message}");
            }

            var lockKey = $"appointment:doctor:{doctor.Id}:time:{scheduledTime.Value:yyyyMMddHHmm}";

            await using var lockHandle = await _lockService.AcquireLockAsync(
                lockKey,
                TimeSpan.FromSeconds(30), // Hold lock for max 30 seconds
                cancellationToken);

            if (lockHandle == null)
            {
                return Result<int>.Failure(
                    "Another booking is in progress for this time slot. Please try again in a moment.");
            }

            // 4. Check doctor availability
            var existingAppointments = await _unitOfWork.Appointments
                .GetByDoctorAndDateAsync(doctor.Id, scheduledTime.Value.Date, cancellationToken);

            if (!doctor.IsAvailable(scheduledTime, existingAppointments))
            {
                return Result<int>.Failure(
                    $"Doctor {doctor.FullName} is not available at {scheduledTime.ToDisplayString()}.");
            }

            // 5. Create appointment entity (validates more business rules)
            Appointment appointment;
            try
            {
                appointment = Appointment.Create(patient, doctor, scheduledTime, command.Reason);
            }
            catch (Exception ex)
            {
                return Result<int>.Failure($"Failed to create appointment: {ex.Message}");
            }

            // 6. Persist appointment
            await _unitOfWork.Appointments.AddAsync(appointment, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 7. Dispatch domain events (Observer pattern)
            await _eventDispatcher.DispatchAsync(appointment.DomainEvents, cancellationToken);
            appointment.ClearDomainEvents();

            // 8. Return success with appointment ID
            return Result<int>.Success(appointment.Id);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure($"An unexpected error occurred: {ex.Message}");
        }
    }
}