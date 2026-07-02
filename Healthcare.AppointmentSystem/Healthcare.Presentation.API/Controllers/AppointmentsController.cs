using Healthcare.Application.Commands.BookAppointment;
using Healthcare.Application.Commands.CancelAppointment;
using Healthcare.Application.Commands.ConfirmAppointment;
using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Presentation.API.Requests;
using Healthcare.Presentation.API.Responses;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Healthcare.Application.Builders;
using Healthcare.Application.Ports.Facades;
using Healthcare.Presentation.API.Authorization;

namespace Healthcare.Presentation.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public sealed class AppointmentsController : ControllerBase
{
    private readonly ICommandHandler<BookAppointmentCommand, Result<int>> _bookAppointmentHandler;
    private readonly ICommandHandler<ConfirmAppointmentCommand, Result> _confirmAppointmentHandler;
    private readonly ICommandHandler<CancelAppointmentCommand, Result> _cancelAppointmentHandler;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppointmentsController> _logger;
    private readonly IAppointmentFacade _facade;

    public AppointmentsController(
        ICommandHandler<BookAppointmentCommand, Result<int>> bookAppointmentHandler,
        ICommandHandler<ConfirmAppointmentCommand, Result> confirmAppointmentHandler,
        ICommandHandler<CancelAppointmentCommand, Result> cancelAppointmentHandler,
        IUnitOfWork unitOfWork,
        ILogger<AppointmentsController> logger,
        IAppointmentFacade facade)
    {
        _bookAppointmentHandler = bookAppointmentHandler;
        _confirmAppointmentHandler = confirmAppointmentHandler;
        _cancelAppointmentHandler = cancelAppointmentHandler;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _facade = facade;
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Patient)]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BookAppointment(
        [FromBody] BookAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Booking appointment Patient:{PatientId} Doctor:{DoctorId}",
            request.PatientId, request.DoctorId);

        var result = await _facade.BookAppointmentAsync(
            patientId: request.PatientId,
            doctorId: request.DoctorId,
            scheduledTime: request.ScheduledTime,
            reason: request.Reason,
            appointmentType: request.AppointmentType,
            cancellationToken: cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Booking failed: {Error}", result.Error);
            return BadRequest(ApiResponse<AppointmentDto>.ErrorResponse(
                result.Error, "Failed to book appointment"));
        }

        return CreatedAtAction(
            nameof(GetAppointmentById),
            new { id = result.Value!.Id },
            ApiResponse<AppointmentDto>.SuccessResponse(
                result.Value, "Appointment booked successfully"));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAppointmentById(
        int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving appointment {AppointmentId}", id);

        var appointment = await _unitOfWork.Appointments.GetByIdAsync(id, cancellationToken);
        if (appointment == null)
        {
            _logger.LogWarning("Appointment {AppointmentId} not found", id);
            return NotFound(ApiResponse<AppointmentDto>.ErrorResponse(
                $"Appointment with ID {id} not found", "Appointment not found"));
        }

        return Ok(ApiResponse<AppointmentDto>.SuccessResponse(MapToDto(appointment)));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<AppointmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAppointments(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving all appointments");

        var appointments = await _unitOfWork.Appointments.GetAllAsync(cancellationToken);
        var mappedList = appointments.Select(MapToDto).ToList();

        return Ok(ApiResponse<List<AppointmentDto>>.SuccessResponse(
            mappedList, $"Retrieved {mappedList.Count} appointment(s)"));
    }

    [HttpGet("patient/{patientId}")]
    [ProducesResponseType(typeof(ApiResponse<List<AppointmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAppointmentsByPatient(
        int patientId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving appointments for Patient {PatientId}", patientId);

        var appointments = await _unitOfWork.Appointments
            .GetByPatientIdAsync(patientId, cancellationToken);
        var mappedList = appointments.Select(MapToDto).ToList();

        return Ok(ApiResponse<List<AppointmentDto>>.SuccessResponse(
            mappedList, $"Retrieved {mappedList.Count} appointment(s) for patient"));
    }

    [HttpGet("doctor/{doctorId}")]
    [ProducesResponseType(typeof(ApiResponse<List<AppointmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAppointmentsByDoctor(
        int doctorId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving appointments for Doctor {DoctorId}", doctorId);

        var appointments = await _unitOfWork.Appointments
            .GetByDoctorIdAsync(doctorId, cancellationToken);
        var mappedList = appointments.Select(MapToDto).ToList();

        return Ok(ApiResponse<List<AppointmentDto>>.SuccessResponse(
            mappedList, $"Retrieved {mappedList.Count} appointment(s) for doctor"));
    }

    [HttpPut("{id}/confirm")]
    [Authorize(Roles = AppRoles.DoctorOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmAppointment(
        int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Confirming appointment {AppointmentId}", id);

        var command = new ConfirmAppointmentCommand { AppointmentId = id };
        var result = await _confirmAppointmentHandler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Confirm failed for {AppointmentId}: {Error}", id, result.Error);

            if (result.Error.Contains("not found"))
                return NotFound(ApiResponse.ErrorResponse(result.Error, "Appointment not found"));

            return BadRequest(ApiResponse.ErrorResponse(
                result.Error, "Failed to confirm appointment"));
        }

        _logger.LogInformation("Appointment {AppointmentId} confirmed successfully", id);
        return Ok(ApiResponse.SuccessResponse("Appointment confirmed successfully"));
    }

    [HttpPut("{id}/cancel")]
    [Authorize(Roles = AppRoles.PatientOrDoctorOrAdmin)]
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
            CancellationReason = request.CancellationReason  // correct property name
        };

        var result = await _cancelAppointmentHandler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to cancel appointment {AppointmentId}: {Error}",
                id, result.Error);

            if (result.Error.Contains("not found"))
                return NotFound(ApiResponse.ErrorResponse(result.Error, "Appointment not found"));

            return BadRequest(ApiResponse.ErrorResponse(
                result.Error, "Failed to cancel appointment"));
        }

        _logger.LogInformation("Appointment {AppointmentId} cancelled successfully", id);
        return Ok(ApiResponse.SuccessResponse("Appointment cancelled successfully"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAppointment(
        int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting appointment {AppointmentId}", id);

        var appointment = await _unitOfWork.Appointments.GetByIdAsync(id, cancellationToken);
        if (appointment == null)
        {
            _logger.LogWarning("Appointment {AppointmentId} not found", id);
            return NotFound(ApiResponse.ErrorResponse(
                $"Appointment with ID {id} not found", "Appointment not found"));
        }

        await _unitOfWork.Appointments.DeleteAsync(id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appointment {AppointmentId} deleted successfully", id);
        return NoContent();
    }

    private static AppointmentDto MapToDto(Domain.Entities.Appointment appointment)
    {
        if (appointment.Patient == null || appointment.Doctor == null)
            throw new InvalidOperationException(
                "Appointment must have Patient and Doctor loaded.");

        return new AppointmentDto
        {
            Id = appointment.Id,
            ReferenceCode = appointment.ReferenceCode,
            PatientId = appointment.PatientId,
            DoctorId = appointment.DoctorId,
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
                Specialties = appointment.Doctor.Specialties
                    .Select(s => s.ToString()).ToList(),
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
