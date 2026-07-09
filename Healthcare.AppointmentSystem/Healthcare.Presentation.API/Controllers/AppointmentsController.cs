using System.Security.Claims;
using System.Text;
using Healthcare.Application.Commands.BookAppointment;
using Healthcare.Application.Commands.CancelAppointment;
using Healthcare.Application.Commands.CompleteAppointment;
using Healthcare.Application.Commands.ConfirmAppointment;
using Healthcare.Application.Commands.MarkNoShowAppointment;
using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Mappings;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Queries.GetAppointment;
using Healthcare.Application.Services;
using Healthcare.Presentation.API.Requests;
using Healthcare.Presentation.API.Responses;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Healthcare.Application.Ports.Events;

using Healthcare.Domain.Enums;
using Healthcare.Domain.Events;
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
    private readonly ICommandHandler<CompleteAppointmentCommand, Result> _completeAppointmentHandler;
    private readonly ICommandHandler<MarkNoShowAppointmentCommand, Result> _markNoShowAppointmentHandler;
    private readonly IQueryHandler<GetAppointmentQuery, Result<AppointmentDto>> _getAppointmentHandler;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppointmentsController> _logger;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public AppointmentsController(
        ICommandHandler<BookAppointmentCommand, Result<int>> bookAppointmentHandler,
        ICommandHandler<ConfirmAppointmentCommand, Result> confirmAppointmentHandler,
        ICommandHandler<CancelAppointmentCommand, Result> cancelAppointmentHandler,
        ICommandHandler<CompleteAppointmentCommand, Result> completeAppointmentHandler,
        ICommandHandler<MarkNoShowAppointmentCommand, Result> markNoShowAppointmentHandler,
        IQueryHandler<GetAppointmentQuery, Result<AppointmentDto>> getAppointmentHandler,
        IUnitOfWork unitOfWork,
        ILogger<AppointmentsController> logger,
        IDomainEventDispatcher eventDispatcher)
    {
        _bookAppointmentHandler = bookAppointmentHandler;
        _confirmAppointmentHandler = confirmAppointmentHandler;
        _cancelAppointmentHandler = cancelAppointmentHandler;
        _completeAppointmentHandler = completeAppointmentHandler;
        _markNoShowAppointmentHandler = markNoShowAppointmentHandler;
        _getAppointmentHandler = getAppointmentHandler;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _eventDispatcher = eventDispatcher;
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

        var command = new BookAppointmentCommand
        {
            PatientId = request.PatientId,
            DoctorId = request.DoctorId,
            ScheduledTime = request.ScheduledTime,
            Reason = request.Reason,
            AppointmentType = request.AppointmentType switch
            {
                "Insurance" => AppointmentType.Insurance,
                "Emergency" => AppointmentType.Emergency,
                "Vip" => AppointmentType.Vip,
                _ => AppointmentType.Standard
            }
        };

        var handlerResult = await _bookAppointmentHandler.HandleAsync(command, cancellationToken);

        if (handlerResult.IsFailure)
        {
            _logger.LogWarning("Booking failed: {Error}", handlerResult.Error);
            return BadRequest(ApiResponse<AppointmentDto>.ErrorResponse(
                handlerResult.Error, "Failed to book appointment"));
        }

        var appointment = await _unitOfWork.Appointments
            .GetByIdAsync(handlerResult.Value, cancellationToken);

        if (appointment is null)
            return BadRequest(ApiResponse<AppointmentDto>.ErrorResponse(
                "Appointment created but could not be retrieved.", "Failed to book appointment"));

        var dto = AppointmentMapper.ToDto(appointment);
        return CreatedAtAction(
            nameof(GetAppointmentById),
            new { id = dto.Id },
            ApiResponse<AppointmentDto>.SuccessResponse(
                dto, "Appointment booked successfully"));
    }

    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAppointmentById(
        int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving appointment {AppointmentId}", id);

        var query = new GetAppointmentQuery(id);
        var result = await _getAppointmentHandler.HandleAsync(query, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Appointment {AppointmentId} not found", id);
            return NotFound(ApiResponse<AppointmentDto>.ErrorResponse(
                result.Error, "Appointment not found"));
        }

        var role = User.GetRole();
        if (role == AppRoles.Patient && User.GetPatientId() != result.Value!.PatientId)
            return Forbid();
        if (role == AppRoles.Doctor && User.GetDoctorId() != result.Value!.DoctorId)
            return Forbid();

        return Ok(ApiResponse<AppointmentDto>.SuccessResponse(result.Value));
    }

    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AppointmentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllAppointments(
    [FromQuery] int pageNumber = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all appointments - Page: {Page}, Size: {Size}", pageNumber, pageSize);

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var pagedEntities = await _unitOfWork.Appointments
            .GetPagedAsync(pageNumber, pageSize, cancellationToken);
        var pagedResult = new PagedResult<AppointmentDto>(
            pagedEntities.Items.Select(AppointmentMapper.ToDto),
            pagedEntities.PageNumber,
            pagedEntities.PageSize,
            pagedEntities.TotalCount);

        return Ok(ApiResponse<PagedResult<AppointmentDto>>.SuccessResponse(
            pagedResult,
            $"Retrieved page {pageNumber} of {pagedResult.TotalPages} ({pagedResult.Items.Count()} appointment(s))"));
    }

    /// <summary>
    /// Gets paginated list of appointments for a patient.
    /// </summary>
    [HttpGet("patient/{patientId}")]
    [Authorize(Roles = AppRoles.PatientOrDoctorOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AppointmentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAppointmentsByPatient(
        int patientId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving appointments for Patient {PatientId} - Page: {Page}, Size: {Size}",
            patientId, pageNumber, pageSize);

        if (User.GetRole() == AppRoles.Patient && User.GetPatientId() != patientId)
            return Forbid();

        // ── Read-Access Audit ──────────────────────────────────────────────
        // Skip audit for self-access (Patient role viewing own appointments).
        // Only non-Patient roles (Doctor, Admin) are logged.
        if (User.GetRole() != AppRoles.Patient)
        {
            int? accessorId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null;

            await _eventDispatcher.DispatchAsync(new PatientRecordAccessedEvent(
                patientId,
                accessorId,
                "Patient appointments retrieved via GetAppointmentsByPatient API"), cancellationToken);
        }

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var pagedEntities = await _unitOfWork.Appointments
            .GetPagedByPatientIdAsync(patientId, pageNumber, pageSize, cancellationToken);
        var pagedResult = new PagedResult<AppointmentDto>(
            pagedEntities.Items.Select(AppointmentMapper.ToDto),
            pagedEntities.PageNumber,
            pagedEntities.PageSize,
            pagedEntities.TotalCount);

        return Ok(ApiResponse<PagedResult<AppointmentDto>>.SuccessResponse(
            pagedResult,
            $"Retrieved page {pageNumber} of {pagedResult.TotalPages} ({pagedResult.Items.Count()} appointment(s)) for patient"));
    }

    /// <summary>
    /// Gets paginated list of appointments for a doctor.
    /// </summary>
    [HttpGet("doctor/{doctorId}")]
    [Authorize(Roles = AppRoles.DoctorOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AppointmentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAppointmentsByDoctor(
        int doctorId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving appointments for Doctor {DoctorId} - Page: {Page}, Size: {Size}",
            doctorId, pageNumber, pageSize);

        if (User.GetRole() == AppRoles.Doctor && User.GetDoctorId() != doctorId)
            return Forbid();

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var pagedEntities = await _unitOfWork.Appointments
            .GetPagedByDoctorIdAsync(doctorId, pageNumber, pageSize, cancellationToken);
        var pagedResult = new PagedResult<AppointmentDto>(
            pagedEntities.Items.Select(AppointmentMapper.ToDto),
            pagedEntities.PageNumber,
            pagedEntities.PageSize,
            pagedEntities.TotalCount);

        return Ok(ApiResponse<PagedResult<AppointmentDto>>.SuccessResponse(
            pagedResult,
            $"Retrieved page {pageNumber} of {pagedResult.TotalPages} ({pagedResult.Items.Count()} appointment(s)) for doctor"));
    }

    [HttpPut("{id}/confirm")]
    [Authorize(Roles = AppRoles.DoctorOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ConfirmAppointment(
        int id,
        [FromBody] ConfirmAppointmentRequest? request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Confirming appointment {AppointmentId}", id);

        var appointment = await _unitOfWork.Appointments.GetByIdAsync(id, cancellationToken);
        if (appointment == null)
        {
            _logger.LogWarning("Appointment {AppointmentId} not found", id);
            return NotFound(ApiResponse.ErrorResponse(
                $"Appointment with ID {id} not found", "Appointment not found"));
        }

        if (User.GetRole() == AppRoles.Doctor && User.GetDoctorId() != appointment.DoctorId)
            return Forbid();

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

        if (request?.OverridePaymentRequirement == true)
        {
            _logger.LogWarning(
                "Appointment {AppointmentId} confirmed WITHOUT payment via Doctor/Admin override. Reason: {Reason}",
                id, request?.OverrideReason);
        }

        _logger.LogInformation("Appointment {AppointmentId} confirmed successfully", id);
        return Ok(ApiResponse.SuccessResponse("Appointment confirmed successfully"));
    }

    [HttpPut("{id}/cancel")]
    [Authorize(Roles = AppRoles.PatientOrDoctorOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CancelAppointment(
        int id,
        [FromBody] CancelAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cancelling appointment {AppointmentId}", id);

        var appointment = await _unitOfWork.Appointments.GetByIdAsync(id, cancellationToken);
        if (appointment == null)
        {
            _logger.LogWarning("Appointment {AppointmentId} not found", id);
            return NotFound(ApiResponse.ErrorResponse(
                $"Appointment with ID {id} not found", "Appointment not found"));
        }

        var role = User.GetRole();
        if (role == AppRoles.Patient && User.GetPatientId() != appointment.PatientId)
            return Forbid();
        if (role == AppRoles.Doctor && User.GetDoctorId() != appointment.DoctorId)
            return Forbid();

        var cancelCommand = new CancelAppointmentCommand
        {
            AppointmentId = id,
            CancellationReason = request.CancellationReason
        };

        var result = await _cancelAppointmentHandler.HandleAsync(cancelCommand, cancellationToken);

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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

        if (User.GetRole() == AppRoles.Doctor && User.GetDoctorId() != appointment.DoctorId)
            return Forbid();

        var command = new CompleteAppointmentCommand
        {
            AppointmentId = id,
            DoctorNotes = request.DoctorNotes
        };

        var result = await _completeAppointmentHandler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to complete appointment {AppointmentId}: {Error}",
                id, result.Error);

            if (result.Error.Contains("not found"))
                return NotFound(ApiResponse.ErrorResponse(result.Error, "Appointment not found"));

            return BadRequest(ApiResponse.ErrorResponse(
                result.Error, "Failed to complete appointment"));
        }

        _logger.LogInformation("Appointment {AppointmentId} completed successfully", id);
        return Ok(ApiResponse.SuccessResponse("Appointment completed successfully"));
    }

    [HttpPut("{id}/mark-no-show")]
    [Authorize(Roles = AppRoles.DoctorOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

        if (User.GetRole() == AppRoles.Doctor && User.GetDoctorId() != appointment.DoctorId)
            return Forbid();

        var command = new MarkNoShowAppointmentCommand
        {
            AppointmentId = id
        };

        var result = await _markNoShowAppointmentHandler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to mark appointment {AppointmentId} as no-show: {Error}",
                id, result.Error);

            if (result.Error.Contains("not found"))
                return NotFound(ApiResponse.ErrorResponse(result.Error, "Appointment not found"));

            return BadRequest(ApiResponse.ErrorResponse(
                result.Error, "Failed to mark appointment as no-show"));
        }

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

        appointment.Delete();
        await _unitOfWork.Appointments.UpdateAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appointment {AppointmentId} soft-deleted successfully", id);
        return NoContent();
    }

}
