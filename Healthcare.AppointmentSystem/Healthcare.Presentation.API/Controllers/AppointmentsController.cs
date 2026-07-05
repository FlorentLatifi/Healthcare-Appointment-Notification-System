using System.Security.Claims;
using System.Text;
using Healthcare.Application.Commands.BookAppointment;
using Healthcare.Application.Commands.CompleteAppointment;
using Healthcare.Application.Commands.MarkNoShowAppointment;
using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Mappings;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Queries.GetAppointment;
using Healthcare.Application.Queries.GetAppointmentsByPatient;
using Healthcare.Application.Services;
using Healthcare.Presentation.API.Requests;
using Healthcare.Presentation.API.Responses;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
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
    private readonly ICommandHandler<CompleteAppointmentCommand, Result> _completeAppointmentHandler;
    private readonly ICommandHandler<MarkNoShowAppointmentCommand, Result> _markNoShowAppointmentHandler;
    private readonly IQueryHandler<GetAppointmentQuery, Result<AppointmentDto>> _getAppointmentHandler;
    private readonly IQueryHandler<GetAppointmentsByPatientQuery, Result<IEnumerable<AppointmentDto>>> _getAppointmentsByPatientHandler;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppointmentsController> _logger;
    private readonly IAppointmentFacade _facade;

    public AppointmentsController(
        ICommandHandler<BookAppointmentCommand, Result<int>> bookAppointmentHandler,
        ICommandHandler<CompleteAppointmentCommand, Result> completeAppointmentHandler,
        ICommandHandler<MarkNoShowAppointmentCommand, Result> markNoShowAppointmentHandler,
        IQueryHandler<GetAppointmentQuery, Result<AppointmentDto>> getAppointmentHandler,
        IQueryHandler<GetAppointmentsByPatientQuery, Result<IEnumerable<AppointmentDto>>> getAppointmentsByPatientHandler,
        IUnitOfWork unitOfWork,
        ILogger<AppointmentsController> logger,
        IAppointmentFacade facade)
    {
        _bookAppointmentHandler = bookAppointmentHandler;
        _completeAppointmentHandler = completeAppointmentHandler;
        _markNoShowAppointmentHandler = markNoShowAppointmentHandler;
        _getAppointmentHandler = getAppointmentHandler;
        _getAppointmentsByPatientHandler = getAppointmentsByPatientHandler;
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

        var query = new GetAppointmentQuery(id);
        var result = await _getAppointmentHandler.HandleAsync(query, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Appointment {AppointmentId} not found", id);
            return NotFound(ApiResponse<AppointmentDto>.ErrorResponse(
                result.Error, "Appointment not found"));
        }

        return Ok(ApiResponse<AppointmentDto>.SuccessResponse(result.Value));
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
        var mappedList = appointments.Select(AppointmentMapper.ToDto);

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

        var query = new GetAppointmentsByPatientQuery(patientId);
        var result = await _getAppointmentsByPatientHandler.HandleAsync(query, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(ApiResponse<PagedResult<AppointmentDto>>.ErrorResponse(
                result.Error, "Failed to retrieve appointments"));
        }

        // TODO: shih koment mbi paginimin in-memory te GetAllAppointments.
        var pagedResult = PagedResult<AppointmentDto>.Create(result.Value, pageNumber, pageSize);

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
        var mappedList = appointments.Select(AppointmentMapper.ToDto);

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

        var result = await _facade.ConfirmAppointmentAsync(
            appointmentId: id,
            overridePaymentRequirement: request?.OverridePaymentRequirement ?? false,
            overrideReason: request?.OverrideReason,
            cancellationToken: cancellationToken);

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
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelAppointment(
        int id,
        [FromBody] CancelAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cancelling appointment {AppointmentId}", id);

        var result = await _facade.CancelAppointmentAsync(
            appointmentId: id,
            reason: request.CancellationReason,
            cancellationToken: cancellationToken);

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
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkNoShowAppointment(
        int id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Marking appointment {AppointmentId} as No-Show", id);

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

        await _unitOfWork.Appointments.DeleteAsync(id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appointment {AppointmentId} deleted successfully", id);
        return NoContent();
    }

}
