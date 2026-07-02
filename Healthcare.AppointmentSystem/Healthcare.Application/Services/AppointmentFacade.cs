using Healthcare.Application.Builders;
using Healthcare.Application.Commands.BookAppointment;
using Healthcare.Application.Commands.CancelAppointment;
using Healthcare.Application.Commands.ConfirmAppointment;
using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Facades;
using Healthcare.Application.Ports.Repositories;

namespace Healthcare.Application.Services;

/// <summary>
/// Facade that simplifies complex appointment workflows.
/// </summary>
/// <remarks>
/// Design Pattern: Facade (Structural)
///
/// This class coordinates the following subsystems:
///   1. Builder Pattern  → BookAppointmentCommandBuilder builds the command safely
///   2. Command Pattern  → Handlers execute the business logic
///   3. Strategy Pattern → PricingStrategySelector picks the right pricing (inside BookAppointmentHandler)
///   4. Observer Pattern → DomainEventDispatcher notifies handlers (inside Handlers)
///
/// The Controller sees NONE of this complexity — it just calls facade.BookAppointmentAsync(...)
/// </remarks>
public sealed class AppointmentFacade : IAppointmentFacade
{
    private readonly ICommandHandler<BookAppointmentCommand, Result<int>> _bookHandler;
    private readonly ICommandHandler<ConfirmAppointmentCommand, Result> _confirmHandler;
    private readonly ICommandHandler<CancelAppointmentCommand, Result> _cancelHandler;
    private readonly IUnitOfWork _unitOfWork;

    public AppointmentFacade(
        ICommandHandler<BookAppointmentCommand, Result<int>> bookHandler,
        ICommandHandler<ConfirmAppointmentCommand, Result> confirmHandler,
        ICommandHandler<CancelAppointmentCommand, Result> cancelHandler,
        IUnitOfWork unitOfWork)
    {
        _bookHandler = bookHandler;
        _confirmHandler = confirmHandler;
        _cancelHandler = cancelHandler;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Books an appointment using Builder + Command + Strategy + Observer internally.
    /// </summary>
    public async Task<Result<AppointmentDto>> BookAppointmentAsync(
        int patientId,
        int doctorId,
        DateTime scheduledTime,
        string reason,
        string appointmentType = "Standard",
        CancellationToken cancellationToken = default)
    {
        // ── STEP 1: Builder Pattern ──────────────────────────────────────────
        BookAppointmentCommand command;
        try
        {
            var builder = new BookAppointmentCommandBuilder()
                .ForPatient(patientId)
                .WithDoctor(doctorId)
                .At(scheduledTime)
                .BecauseOf(reason);

            command = appointmentType switch
            {
                "Insurance" => builder.WithInsurance().Build(),
                "Emergency" => builder.AsEmergency().Build(),
                "Vip" => builder.AsVip().Build(),
                _ => builder.AsStandard().Build()
            };
        }
        catch (Exception ex)
        {
            return Result<AppointmentDto>.Failure($"Invalid request: {ex.Message}");
        }
        // ── END BUILDER ──────────────────────────────────────────────────────

        // ── STEP 2: Command Handler (Strategy + Observer inside) ─────────────
        var result = await _bookHandler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
            return Result<AppointmentDto>.Failure(result.Error);
        // ── END COMMAND ──────────────────────────────────────────────────────

        // ── STEP 3: Fetch created appointment and return DTO ─────────────────
        var appointment = await _unitOfWork.Appointments
            .GetByIdAsync(result.Value, cancellationToken);

        if (appointment is null)
            return Result<AppointmentDto>.Failure(
                "Appointment created but could not be retrieved.");

        var dto = new AppointmentDto
        {
            Id = appointment.Id,
            ReferenceCode = appointment.ReferenceCode,
            PatientId = appointment.PatientId,
            DoctorId = appointment.DoctorId,
            ScheduledTime = appointment.ScheduledTime.Value,
            Reason = appointment.Reason,
            Status = appointment.Status.ToString(),
            ConsultationFeeAmount = appointment.ConsultationFee.Amount,
            ConsultationFeeCurrency = appointment.ConsultationFee.Currency
        };

        return Result<AppointmentDto>.Success(dto);
        // ── END FETCH ────────────────────────────────────────────────────────
    }

    /// <summary>
    /// Confirms an appointment.
    /// </summary>
    public async Task<Result> ConfirmAppointmentAsync(
        int appointmentId,
        CancellationToken cancellationToken = default)
    {
        var command = new ConfirmAppointmentCommand { AppointmentId = appointmentId };
        return await _confirmHandler.HandleAsync(command, cancellationToken);
    }

    /// <summary>
    /// Cancels an appointment.
    /// </summary>
    public async Task<Result> CancelAppointmentAsync(
        int appointmentId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var command = new CancelAppointmentCommand
        {
            AppointmentId = appointmentId,
            CancellationReason = reason
        };
        return await _cancelHandler.HandleAsync(command, cancellationToken);
    }
}
