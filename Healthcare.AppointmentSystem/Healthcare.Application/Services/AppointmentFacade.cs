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
/// 
///   1. Builder Pattern
///      → BookAppointmentCommandBuilder builds the command safely
/// 
///   2. Command Pattern
///      → Handlers execute the business logic
/// 
///   3. Strategy Pattern (inside BookAppointmentHandler)
///      → PricingStrategySelector picks the right pricing
/// 
///   4. Observer Pattern (inside Handlers)
///      → DomainEventDispatcher notifies handlers
/// 
/// The Controller sees NONE of this complexity.
/// It just calls: facade.BookAppointmentAsync(...)
/// 
/// BEFORE Facade (Controller had to do all this):
///   var builder = new BookAppointmentCommandBuilder()...
///   var command = builder.Build();
///   var result = await _handler.HandleAsync(command);
///   if (result.IsSuccess) await _notifier.SendAsync(...);
///   ...
/// 
/// AFTER Facade (Controller does just this):
///   var result = await _facade.BookAppointmentAsync(...);
/// </remarks>
public sealed class AppointmentFacade : IAppointmentFacade
{
    private readonly ICommandHandler<BookAppointmentCommand, Result<int>>
        _bookHandler;

    private readonly ICommandHandler<ConfirmAppointmentCommand, Result>
        _confirmHandler;

    private readonly ICommandHandler<CancelAppointmentCommand, Result>
        _cancelHandler;

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
    /// Books an appointment using Builder + Command + Strategy internally.
    /// </summary>
    public async Task<Result<AppointmentDto>> BookAppointmentAsync(
        int patientId,
        int doctorId,
        DateTime scheduledTime,
        string reason,
        string appointmentType = "Standard",
        CancellationToken cancellationToken = default)
    {
        // ── STEP 1: Builder Pattern ──────────────────────
        // Build the command safely — validates each field
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
            return Result<AppointmentDto>.Failure(
                $"Invalid request: {ex.Message}");
        }
        // ── END BUILDER ──────────────────────────────────

        // ── STEP 2: Command Handler ──────────────────────
        // Internally uses Strategy for pricing
        // and Observer for event dispatching
        var result = await _bookHandler.HandleAsync(
            command, cancellationToken);

        if (result.IsFailure)
            return Result<AppointmentDto>.Failure(result.Error);
        // ── END COMMAND ──────────────────────────────────

        // ── STEP 3: Fetch and return DTO ─────────────────
        var appointment = await _unitOfWork.Appointments
            .GetByIdAsync(result.Value, cancellationToken);

        if (appointment is null)
            return Result<AppointmentDto>.Failure(
                "Appointment created but could not be retrieved.");

        var dto = new AppointmentDto
        {
            Id = appointment.Id,
            PatientId = appointment.PatientId,
            DoctorId = appointment.DoctorId,
            ScheduledTime = appointment.ScheduledTime.Value,
            Reason = appointment.Reason,
            Status = appointment.Status.ToString(),
            ConsultationFee = appointment.ConsultationFee.Amount,
            Currency = appointment.ConsultationFee.Currency
        };

        return Result<AppointmentDto>.Success(dto);
        // ── END FETCH ─────────────────────────────────────
    }

    /// <summary>
    /// Confirms an appointment.
    /// </summary>
    public async Task<Result> ConfirmAppointmentAsync(
        int appointmentId,
        CancellationToken cancellationToken = default)
    {
        var command = new ConfirmAppointmentCommand
        {
            AppointmentId = appointmentId
        };

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