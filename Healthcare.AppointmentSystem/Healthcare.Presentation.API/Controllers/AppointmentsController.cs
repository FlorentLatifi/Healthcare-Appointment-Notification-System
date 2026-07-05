using System.Security.Claims;
using System.Text;
using Healthcare.Application.Commands.BookAppointment;
using Healthcare.Application.Commands.CancelAppointment;
using Healthcare.Application.Commands.ConfirmAppointment;
using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Services;
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
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AppointmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAppointments(
    [FromQuery] int pageNumber = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all appointments - Page: {Page}, Size: {Size}", pageNumber, pageSize);

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        // TODO: Paginimi aktualisht bëhet in-memory (Skip/Take mbi IEnumerable<Appointment>
        // të kthyer nga GetAllAsync). Duhet migruar në DB-level (IQueryable.Skip/Take
        // përpara ToListAsync) kur repository-t të mbështesin queryable.
        var appointments = await _unitOfWork.Appointments.GetAllAsync(cancellationToken);
        var mappedList = appointments.Select(MapToDto);

        var pagedResult = PagedResult<AppointmentDto>.Create(mappedList, pageNumber, pageSize);

        return Ok(ApiResponse<PagedResult<AppointmentDto>>.SuccessResponse(
            pagedResult,
            $"Retrieved page {pageNumber} of {pagedResult.TotalPages} ({pagedResult.Items.Count()} appointment(s))"));
    }

    /// <summary>
    /// Gets paginated list of appointments for a patient.
    /// </summary>
    [HttpGet("patient/{patientId}")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AppointmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAppointmentsByPatient(
        int patientId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving appointments for Patient {PatientId} - Page: {Page}, Size: {Size}",
            patientId, pageNumber, pageSize);

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        // TODO: shih koment mbi paginimin in-memory te GetAllAppointments.
        var appointments = await _unitOfWork.Appointments
            .GetByPatientIdAsync(patientId, cancellationToken);
        var mappedList = appointments.Select(MapToDto);

        var pagedResult = PagedResult<AppointmentDto>.Create(mappedList, pageNumber, pageSize);

        return Ok(ApiResponse<PagedResult<AppointmentDto>>.SuccessResponse(
            pagedResult,
            $"Retrieved page {pageNumber} of {pagedResult.TotalPages} ({pagedResult.Items.Count()} appointment(s)) for patient"));
    }

    /// <summary>
    /// Gets paginated list of appointments for a doctor.
    /// </summary>
    [HttpGet("doctor/{doctorId}")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AppointmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAppointmentsByDoctor(
        int doctorId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving appointments for Doctor {DoctorId} - Page: {Page}, Size: {Size}",
            doctorId, pageNumber, pageSize);

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        // TODO: shih koment mbi paginimin in-memory te GetAllAppointments.
        var appointments = await _unitOfWork.Appointments
            .GetByDoctorIdAsync(doctorId, cancellationToken);
        var mappedList = appointments.Select(MapToDto);

        var pagedResult = PagedResult<AppointmentDto>.Create(mappedList, pageNumber, pageSize);

        return Ok(ApiResponse<PagedResult<AppointmentDto>>.SuccessResponse(
            pagedResult,
            $"Retrieved page {pageNumber} of {pagedResult.TotalPages} ({pagedResult.Items.Count()} appointment(s)) for doctor"));
    }

    [HttpPut("{id}/confirm")]
    [Authorize(Roles = AppRoles.DoctorOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmAppointment(
        int id,
        [FromBody] ConfirmAppointmentRequest? request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Confirming appointment {AppointmentId}", id);

        var command = new ConfirmAppointmentCommand
        {
            AppointmentId = id,
            OverridePaymentRequirement = request?.OverridePaymentRequirement ?? false,
            OverrideReason = request?.OverrideReason
        };

        var result = await _confirmAppointmentHandler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Confirm failed for {AppointmentId}: {Error}", id, result.Error);

            if (result.Error.Contains("not found"))
                return NotFound(ApiResponse.ErrorResponse(result.Error, "Appointment not found"));

            return BadRequest(ApiResponse.ErrorResponse(
                result.Error, "Failed to confirm appointment"));
        }

        if (command.OverridePaymentRequirement)
        {
            _logger.LogWarning(
                "Appointment {AppointmentId} confirmed WITHOUT payment via Doctor/Admin override. Reason: {Reason}",
                id, command.OverrideReason);
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

    [HttpGet("{id}/calendar.ics")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCalendarIcs(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Exporting ICS calendar for appointment {AppointmentId}", id);

        var appointment = await _unitOfWork.Appointments.GetByIdAsync(id, cancellationToken);
        if (appointment == null)
        {
            _logger.LogWarning("Appointment {AppointmentId} not found", id);
            return NotFound(ApiResponse.ErrorResponse(
                $"Appointment with ID {id} not found", "Appointment not found"));
        }

        // Authorize: only the patient or doctor of this appointment may export
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var isOwner = (appointment.Patient?.Email?.Value?.Equals(userEmail, StringComparison.OrdinalIgnoreCase) == true)
                   || (appointment.Doctor?.Email?.Value?.Equals(userEmail, StringComparison.OrdinalIgnoreCase) == true);
        if (!isOwner)
        {
            _logger.LogWarning("User {Email} not authorized to export ICS for appointment {AppointmentId}", userEmail, id);
            return Forbid();
        }

        var icsContent = IcsCalendarService.GenerateAppointmentIcs(appointment);
        return Content(icsContent, "text/calendar", Encoding.UTF8);
    }

    [HttpPut("{id}/complete")]
    [Authorize(Roles = AppRoles.DoctorOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteAppointment(
        int id,
        [FromBody] CompleteAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Completing appointment {AppointmentId}", id);

        var appointment = await _unitOfWork.Appointments.GetByIdAsync(id, cancellationToken);
        if (appointment == null)
        {
            _logger.LogWarning("Appointment {AppointmentId} not found", id);
            return NotFound(ApiResponse.ErrorResponse(
                $"Appointment with ID {id} not found", "Appointment not found"));
        }

        appointment.Complete(request.DoctorNotes);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appointment {AppointmentId} completed successfully", id);
        return Ok(ApiResponse.SuccessResponse("Appointment completed successfully"));
    }

    [HttpPut("{id}/mark-no-show")]
    [Authorize(Roles = AppRoles.DoctorOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkNoShowAppointment(
        int id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Marking appointment {AppointmentId} as No-Show", id);

        var appointment = await _unitOfWork.Appointments.GetByIdAsync(id, cancellationToken);
        if (appointment == null)
        {
            _logger.LogWarning("Appointment {AppointmentId} not found", id);
            return NotFound(ApiResponse.ErrorResponse(
                $"Appointment with ID {id} not found", "Appointment not found"));
        }

        appointment.MarkAsNoShow();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appointment {AppointmentId} marked as No-Show", id);
        return Ok(ApiResponse.SuccessResponse("Appointment marked as No-Show"));
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
