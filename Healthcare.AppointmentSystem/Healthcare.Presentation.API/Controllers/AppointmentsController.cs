using Healthcare.Application.Commands.BookAppointment;
using Healthcare.Application.Commands.CancelAppointment;
using Healthcare.Application.Commands.ConfirmAppointment;
using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Presentation.API.Requests;
using Healthcare.Presentation.API.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Healthcare.Presentation.API.Controllers;

/// <summary>
/// Controller for managing appointments.
/// </summary>
/// <remarks>
/// Design Pattern: MVC Pattern + REST Architecture
/// 
/// This controller:
/// - Exposes REST endpoints for appointment operations
/// - Validates input via FluentValidation
/// - Delegates business logic to Application Layer (Command/Query Handlers)
/// - Returns standardized API responses
/// - Handles HTTP status codes appropriately
/// 
/// REST Endpoints:
/// - POST   /api/appointments          - Book new appointment
/// - GET    /api/appointments/{id}     - Get appointment by ID
/// - GET    /api/appointments          - Get all appointments
/// - GET    /api/appointments/patient/{patientId} - Get by patient
/// - GET    /api/appointments/doctor/{doctorId}   - Get by doctor
/// - PUT    /api/appointments/{id}/confirm - Confirm appointment
/// - PUT    /api/appointments/{id}/cancel  - Cancel appointment
/// - DELETE /api/appointments/{id}     - Delete appointment
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class AppointmentsController : ControllerBase
{
    private readonly ICommandHandler<BookAppointmentCommand, Result<int>> _bookAppointmentHandler;
    private readonly ICommandHandler<ConfirmAppointmentCommand, Result> _confirmAppointmentHandler;
    private readonly ICommandHandler<CancelAppointmentCommand, Result> _cancelAppointmentHandler;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppointmentsController> _logger;

    public AppointmentsController(
        ICommandHandler<BookAppointmentCommand, Result<int>> bookAppointmentHandler,
        ICommandHandler<ConfirmAppointmentCommand, Result> confirmAppointmentHandler,
        ICommandHandler<CancelAppointmentCommand, Result> cancelAppointmentHandler,
        IUnitOfWork unitOfWork,
        ILogger<AppointmentsController> logger)
    {
        _bookAppointmentHandler = bookAppointmentHandler;
        _confirmAppointmentHandler = confirmAppointmentHandler;
        _cancelAppointmentHandler = cancelAppointmentHandler;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Books a new appointment.
    /// </summary>
    /// <param name="request">The appointment booking details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ID of the created appointment.</returns>
    /// <response code="201">Appointment booked successfully.</response>
    /// <response code="400">Invalid request data or business rule violation.</response>
    /// <response code="500">Internal server error.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BookAppointment(
        [FromBody] BookAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Booking appointment for Patient {PatientId} with Doctor {DoctorId} at {Time}",
            request.PatientId,
            request.DoctorId,
            request.ScheduledTime);

        var command = new BookAppointmentCommand
        {
            PatientId = request.PatientId,
            DoctorId = request.DoctorId,
            ScheduledTime = request.ScheduledTime,
            Reason = request.Reason
        };

        var result = await _bookAppointmentHandler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to book appointment: {Error}", result.Error);
            return BadRequest(ApiResponse<int>.ErrorResponse(result.Error, "Failed to book appointment"));
        }

        _logger.LogInformation("Appointment {AppointmentId} booked successfully", result.Value);
        return CreatedAtAction(
            nameof(GetAppointmentById),
            new { id = result.Value },
            ApiResponse<int>.SuccessResponse(result.Value, "Appointment booked successfully"));
    }

    /// <summary>
    /// Gets an appointment by ID.
    /// </summary>
    /// <param name="id">The appointment ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The appointment details.</returns>
    /// <response code="200">Appointment found.</response>
    /// <response code="404">Appointment not found.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAppointmentById(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving appointment {AppointmentId}", id);

        var appointment = await _unitOfWork.Appointments.GetByIdAsync(id, cancellationToken);

        if (appointment == null)
        {
            _logger.LogWarning("Appointment {AppointmentId} not found", id);
            return NotFound(ApiResponse<AppointmentDto>.ErrorResponse(
                $"Appointment with ID {id} not found",
                "Appointment not found"));
        }

        var dto = MapToDto(appointment);
        return Ok(ApiResponse<AppointmentDto>.SuccessResponse(dto));
    }

    /// <summary>
    /// Gets all appointments.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all appointments.</returns>
    /// <response code="200">Appointments retrieved successfully.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<AppointmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAppointments(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving all appointments");

        var appointments = await _unitOfWork.Appointments.GetAllAsync(cancellationToken);
        var dtos = appointments.Select(MapToDto).ToList();

        return Ok(ApiResponse<List<AppointmentDto>>.SuccessResponse(
            dtos,
            $"Retrieved {dtos.Count} appointment(s)"));
    }

    /// <summary>
    /// Gets appointments for a specific patient.
    /// </summary>
    /// <param name="patientId">The patient ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of patient's appointments.</returns>
    /// <response code="200">Appointments retrieved successfully.</response>
    [HttpGet("patient/{patientId}")]
    [ProducesResponseType(typeof(ApiResponse<List<AppointmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAppointmentsByPatient(
        int patientId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving appointments for Patient {PatientId}", patientId);

        var appointments = await _unitOfWork.Appointments
            .GetByPatientIdAsync(patientId, cancellationToken);

        var dtos = appointments.Select(MapToDto).ToList();

        return Ok(ApiResponse<List<AppointmentDto>>.SuccessResponse(
            dtos,
            $"Retrieved {dtos.Count} appointment(s) for patient"));
    }

    /// <summary>
    /// Gets appointments for a specific doctor.
    /// </summary>
    /// <param name="doctorId">The doctor ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of doctor's appointments.</returns>
    /// <response code="200">Appointments retrieved successfully.</response>
    [HttpGet("doctor/{doctorId}")]
    [ProducesResponseType(typeof(ApiResponse<List<AppointmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAppointmentsByDoctor(
        int doctorId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving appointments for Doctor {DoctorId}", doctorId);

        var appointments = await _unitOfWork.Appointments
            .GetByDoctorIdAsync(doctorId, cancellationToken);

        var dtos = appointments.Select(MapToDto).ToList();

        return Ok(ApiResponse<List<AppointmentDto>>.SuccessResponse(
            dtos,
            $"Retrieved {dtos.Count} appointment(s) for doctor"));
    }

    /// <summary>
    /// Confirms an appointment.
    /// </summary>
    /// <param name="id">The appointment ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success or failure result.</returns>
    /// <response code="200">Appointment confirmed successfully.</response>
    /// <response code="400">Invalid request or business rule violation.</response>
    /// <response code="404">Appointment not found.</response>
    [HttpPut("{id}/confirm")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmAppointment(
        int id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Confirming appointment {AppointmentId}", id);

        var command = new ConfirmAppointmentCommand { AppointmentId = id };
        var result = await _confirmAppointmentHandler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to confirm appointment {AppointmentId}: {Error}", id, result.Error);

            if (result.Error.Contains("not found"))
            {
                return NotFound(ApiResponse.ErrorResponse(result.Error, "Appointment not found"));
            }

            return BadRequest(ApiResponse.ErrorResponse(result.Error, "Failed to confirm appointment"));
        }

        _logger.LogInformation("Appointment {AppointmentId} confirmed successfully", id);
        return Ok(ApiResponse.SuccessResponse("Appointment confirmed successfully"));
    }

    /// <summary>
    /// Cancels an appointment.
    /// </summary>
    /// <param name="id">The appointment ID.</param>
    /// <param name="request">The cancellation details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success or failure result.</returns>
    /// <response code="200">Appointment cancelled successfully.</response>
    /// <response code="400">Invalid request or business rule violation.</response>
    /// <response code="404">Appointment not found.</response>
    [HttpPut("{id}/cancel")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelAppointment(
        int id,
        [FromBody] CancelAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cancelling appointment {AppointmentId}", id);

        var command = new CancelAppointmentCommand
        {
            AppointmentId = id,
            CancellationReason = request.CancellationReason
        };

        var result = await _cancelAppointmentHandler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to cancel appointment {AppointmentId}: {Error}", id, result.Error);

            if (result.Error.Contains("not found"))
            {
                return NotFound(ApiResponse.ErrorResponse(result.Error, "Appointment not found"));
            }

            return BadRequest(ApiResponse.ErrorResponse(result.Error, "Failed to cancel appointment"));
        }

        _logger.LogInformation("Appointment {AppointmentId} cancelled successfully", id);
        return Ok(ApiResponse.SuccessResponse("Appointment cancelled successfully"));
    }

    /// <summary>
    /// Deletes an appointment.
    /// </summary>
    /// <param name="id">The appointment ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success or failure result.</returns>
    /// <response code="204">Appointment deleted successfully.</response>
    /// <response code="404">Appointment not found.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAppointment(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting appointment {AppointmentId}", id);

        var appointment = await _unitOfWork.Appointments.GetByIdAsync(id, cancellationToken);
        if (appointment == null)
        {
            _logger.LogWarning("Appointment {AppointmentId} not found", id);
            return NotFound(ApiResponse.ErrorResponse(
                $"Appointment with ID {id} not found",
                "Appointment not found"));
        }

        await _unitOfWork.Appointments.DeleteAsync(id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appointment {AppointmentId} deleted successfully", id);
        return NoContent();
    }

    /// <summary>
    /// Maps Appointment entity to AppointmentDto.
    /// </summary>
    private static AppointmentDto MapToDto(Domain.Entities.Appointment appointment)
    {
        return new AppointmentDto
        {
            Id = appointment.Id,
            Patient = new PatientDto
            {
                Id = appointment.Patient.Id,
                FirstName = appointment.Patient.FirstName,
                LastName = appointment.Patient.LastName,
                FullName = appointment.Patient.FullName,
                Email = appointment.Patient.Email.Value,
                PhoneNumber = appointment.Patient.PhoneNumber.Value,
                DateOfBirth = appointment.Patient.DateOfBirth,
                Age = appointment.Patient.Age,
                Gender = appointment.Patient.Gender.ToString(),
                Address = appointment.Patient.Address.GetFullAddress(),
                IsActive = appointment.Patient.IsActive,
                CreatedAt = appointment.Patient.CreatedAt
            },
            Doctor = new DoctorDto
            {
                Id = appointment.Doctor.Id,
                FirstName = appointment.Doctor.FirstName,
                LastName = appointment.Doctor.LastName,
                FullName = appointment.Doctor.FullName,
                Email = appointment.Doctor.Email.Value,
                PhoneNumber = appointment.Doctor.PhoneNumber.Value,
                LicenseNumber = appointment.Doctor.LicenseNumber,
                Specialties = appointment.Doctor.Specialties.Select(s => s.ToString()).ToList(),
                ConsultationFeeAmount = appointment.Doctor.ConsultationFee.Amount,
                ConsultationFeeCurrency = appointment.Doctor.ConsultationFee.Currency,
                IsAcceptingPatients = appointment.Doctor.IsAcceptingPatients,
                IsActive = appointment.Doctor.IsActive,
                YearsOfExperience = appointment.Doctor.YearsOfExperience,
                CreatedAt = appointment.Doctor.CreatedAt
            },
            ScheduledTime = appointment.ScheduledTime.Value,
            ScheduledDate = appointment.ScheduledTime.GetDate().ToString("yyyy-MM-dd"),
            ScheduledTimeFormatted = appointment.ScheduledTime.ToDisplayString(),
            Status = appointment.Status.ToString(),
            Reason = appointment.Reason,
            DoctorNotes = appointment.DoctorNotes,
            CancellationReason = appointment.CancellationReason,
            ConsultationFeeAmount = appointment.ConsultationFee.Amount,
            ConsultationFeeCurrency = appointment.ConsultationFee.Currency,
            ConfirmedAt = appointment.ConfirmedAt,
            CompletedAt = appointment.CompletedAt,
            CancelledAt = appointment.CancelledAt,
            CreatedAt = appointment.CreatedAt
        };
    }
}