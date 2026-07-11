using Healthcare.Application.Common;
using Healthcare.Application.Ports.Caching;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Locking;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Strategies.Pricing;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Services;
using Healthcare.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.Application.Commands.BookAppointment;

public sealed class BookAppointmentHandler
    : IRequestHandler<BookAppointmentCommand, Result<int>>,
      ICommandHandler<BookAppointmentCommand, Result<int>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly IDistributedLockService _lockService;
    private readonly IAppointmentCodeGenerator _codeGenerator;

    public BookAppointmentHandler(
        IUnitOfWork unitOfWork,
        IDomainEventDispatcher eventDispatcher,
        IDistributedLockService lockService,
        IAppointmentCodeGenerator codeGenerator)
    {
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
        _lockService = lockService;
        _codeGenerator = codeGenerator;
    }

    /// <summary>MediatR entry point.</summary>
    public Task<Result<int>> Handle(BookAppointmentCommand request, CancellationToken cancellationToken)
        => HandleAsync(request, cancellationToken);

    /// <summary>Legacy / unit-test entry point.</summary>
    public async Task<Result<int>> HandleAsync(
        BookAppointmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var patient = await _unitOfWork.Patients
            .GetByIdAsync(command.PatientId, cancellationToken);

        if (patient is null)
            return Result<int>.Failure(
                $"Patient with ID {command.PatientId} not found.");

        var doctor = await _unitOfWork.Doctors
            .GetByIdAsync(command.DoctorId, cancellationToken);

        if (doctor is null)
            return Result<int>.Failure(
                $"Doctor with ID {command.DoctorId} not found.");

        if (!doctor.IsAcceptingPatients)
            return Result<int>.Failure(
                $"Doctor {doctor.FullName} is not accepting patients.");

        AppointmentTime scheduledTime;
        try
        {
            scheduledTime = AppointmentTime.Create(command.ScheduledTime);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure(
                $"Invalid appointment time: {ex.Message}");
        }

        // Logical lock key — Redis adapter prefixes with InstanceName.
        var lockKey = CacheKeys.AppointmentBookingLock(doctor.Id, scheduledTime.Value);

        await using var lockHandle = await _lockService.AcquireLockAsync(
            lockKey,
            TimeSpan.FromSeconds(30),
            cancellationToken);

        if (lockHandle is null)
            return Result<int>.Failure(
                "Another booking is in progress. Please try again.");

        var existingAppointments = await _unitOfWork.Appointments
            .GetByDoctorAndDateAsync(
                doctor.Id,
                scheduledTime.Value.Date,
                cancellationToken);

        if (!doctor.IsAvailable(scheduledTime, existingAppointments))
            return Result<int>.Failure(
                $"Doctor {doctor.FullName} is not available " +
                $"at {scheduledTime.ToDisplayString()}.");

        var strategy = PricingStrategySelector.Select(command.AppointmentType);
        var pricingContext = new PricingContext(strategy);
        var finalPrice = pricingContext.ExecutePricing(
            doctor.ConsultationFee.Amount);

        // Persist with retry on reference-code collision; map double-book unique index to a clean failure.
        Appointment? createdAppointment = null;
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            Appointment appointment;
            try
            {
                appointment = Appointment.Create(
                    patient, doctor, scheduledTime, command.Reason, _codeGenerator);
            }
            catch (Exception ex)
            {
                // e.g. Redis code generator unavailable
                return Result<int>.Failure(ex.Message);
            }

            appointment.ApplyPricingStrategy(
                finalPrice,
                doctor.ConsultationFee.Currency);

            await _unitOfWork.Appointments.AddAsync(appointment, cancellationToken);

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                createdAppointment = appointment;
                break;
            }
            catch (DbUpdateException ex) when (
                DbConstraintErrors.IsUniqueViolation(ex, "IX_Appointments_Doctor_Time_Active", "Doctor_Time_Active"))
            {
                _unitOfWork.ResetChangeTracker();
                return Result<int>.Failure(
                    $"Doctor {doctor.FullName} is not available " +
                    $"at {scheduledTime.ToDisplayString()}.");
            }
            catch (DbUpdateException ex) when (
                attempt == 1 &&
                DbConstraintErrors.IsUniqueViolation(ex, "IX_Appointments_ReferenceCode", "ReferenceCode"))
            {
                _unitOfWork.ResetChangeTracker();
                // Retry once with a freshly generated code
            }
            catch (DbUpdateException ex) when (
                DbConstraintErrors.IsUniqueViolation(ex, "IX_Appointments_ReferenceCode", "ReferenceCode"))
            {
                return Result<int>.Failure(
                    "A unique appointment reference code could not be generated. Please try again.");
            }
        }

        if (createdAppointment is null)
        {
            return Result<int>.Failure(
                "The appointment could not be saved. Please try again.");
        }

        await _eventDispatcher.DispatchAsync(
            createdAppointment.DomainEvents, cancellationToken);
        createdAppointment.ClearDomainEvents();

        return Result<int>.Success(createdAppointment.Id);
    }
}
