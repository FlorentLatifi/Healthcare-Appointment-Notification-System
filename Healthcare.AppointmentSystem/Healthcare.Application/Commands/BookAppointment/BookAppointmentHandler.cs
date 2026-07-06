using Healthcare.Application.Common;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Locking;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Strategies.Pricing;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Services;
using Healthcare.Domain.ValueObjects;

namespace Healthcare.Application.Commands.BookAppointment;

public sealed class BookAppointmentHandler
    : ICommandHandler<BookAppointmentCommand, Result<int>>
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

    public async Task<Result<int>> HandleAsync(
        BookAppointmentCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Fetch patient
            var patient = await _unitOfWork.Patients
                .GetByIdAsync(command.PatientId, cancellationToken);

            if (patient is null)
                return Result<int>.Failure(
                    $"Patient with ID {command.PatientId} not found.");

            // 2. Fetch doctor
            var doctor = await _unitOfWork.Doctors
                .GetByIdAsync(command.DoctorId, cancellationToken);

            if (doctor is null)
                return Result<int>.Failure(
                    $"Doctor with ID {command.DoctorId} not found.");

            if (!doctor.IsAcceptingPatients)
                return Result<int>.Failure(
                    $"Doctor {doctor.FullName} is not accepting patients.");

            // 3. Create AppointmentTime value object
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

            // 4. Acquire distributed lock
            var lockKey =
                $"appointment:doctor:{doctor.Id}:" +
                $"time:{scheduledTime.Value:yyyyMMddHHmm}";

            await using var lockHandle = await _lockService.AcquireLockAsync(
                lockKey,
                TimeSpan.FromSeconds(30),
                cancellationToken);

            if (lockHandle is null)
                return Result<int>.Failure(
                    "Another booking is in progress. Please try again.");

            // 5. Check doctor availability
            var existingAppointments = await _unitOfWork.Appointments
                .GetByDoctorAndDateAsync(
                    doctor.Id,
                    scheduledTime.Value.Date,
                    cancellationToken);

            if (!doctor.IsAvailable(scheduledTime, existingAppointments))
                return Result<int>.Failure(
                    $"Doctor {doctor.FullName} is not available " +
                    $"at {scheduledTime.ToDisplayString()}.");

            // 6. Create appointment entity
            // ── SINGLETON PATTERN ──────────────────────────────────────────────────
               // _codeGenerator is backed by AppointmentCodeGenerator.Instance (Singleton)
              // ONE instance serves ALL requests — unique codes guaranteed application-wide.
            Appointment appointment;
            try
            {
                appointment = Appointment.Create(
                    patient, doctor, scheduledTime, command.Reason, _codeGenerator);
            }
            catch (Exception ex)
            {
                return Result<int>.Failure(ex.Message);
            }

            // ── STRATEGY PATTERN ────────────────────────────────
            // Select strategy based on appointment type
            // PricingContext executes it and returns final price
            var strategy = PricingStrategySelector.Select(command.AppointmentType);
            var pricingContext = new PricingContext(strategy);
            var finalPrice = pricingContext.ExecutePricing(
                doctor.ConsultationFee.Amount);

            // Apply the calculated price to the appointment
            appointment.ApplyPricingStrategy(
                finalPrice,
                doctor.ConsultationFee.Currency);
            // ── END STRATEGY ────────────────────────────────────

            // 7. Persist
            await _unitOfWork.Appointments.AddAsync(appointment, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 8. Dispatch domain events (Observer Pattern)
            await _eventDispatcher.DispatchAsync(
                appointment.DomainEvents, cancellationToken);
            appointment.ClearDomainEvents();

            return Result<int>.Success(appointment.Id);
    }
}