using Asp.Versioning;
using Healthcare.Application.Commands.AnonymizePatient;
using Healthcare.Application.Commands.CreatePatient;
using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Audit;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Audit;
using Healthcare.Domain.Enums;
using Healthcare.Domain.Events;
using Healthcare.Presentation.API.Authorization;
using Healthcare.Presentation.API.Requests;
using Healthcare.Presentation.API.Resources;
using Healthcare.Presentation.API.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Security.Claims;


namespace Healthcare.Presentation.API.Controllers;

/// <summary>
/// Controller for managing patients.
/// </summary>
/// <remarks>
/// REST Endpoints:
/// - POST   /api/patients          - Create new patient
/// - GET    /api/patients/{id}     - Get patient by ID
/// - GET    /api/patients          - Get all patients (paginated)
/// - GET    /api/patients/active   - Get active patients (paginated)
/// - GET    /api/patients/search?term={term} - Search by name (paginated)
/// - DELETE /api/patients/{id}     - Delete patient
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public sealed class PatientsController : ControllerBase
{
    private readonly ICommandHandler<CreatePatientCommand, Result<int>> _createPatientHandler;
    private readonly ICommandHandler<AnonymizePatientCommand, Result> _anonymizePatientHandler;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<Messages> _localizer;
    private readonly ILogger<PatientsController> _logger;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly IAuditLogService _auditLogService;

    public PatientsController(
        ICommandHandler<CreatePatientCommand, Result<int>> createPatientHandler,
        ICommandHandler<AnonymizePatientCommand, Result> anonymizePatientHandler,
        IUnitOfWork unitOfWork,
        IStringLocalizer<Messages> localizer,
        ILogger<PatientsController> logger,
        IDomainEventDispatcher eventDispatcher,
        IAuditLogService auditLogService)
    {
        _createPatientHandler = createPatientHandler;
        _anonymizePatientHandler = anonymizePatientHandler;
        _unitOfWork = unitOfWork;
        _localizer = localizer;
        _logger = logger;
        _eventDispatcher = eventDispatcher;
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// Creates a new patient.
    /// </summary>
    /// <param name="request">The patient details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ID of the created patient.</returns>
    /// <response code="201">Patient created successfully.</response>
    /// <response code="400">Invalid request data or patient already exists.</response>
    [HttpPost]
    [Authorize(Roles = AppRoles.Patient)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreatePatient(
        [FromBody] CreatePatientRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating patient: {Email}", request.Email);

        var command = new CreatePatientCommand
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            Street = request.Street,
            City = request.City,
            State = request.State,
            PostalCode = request.PostalCode,
            Country = request.Country,
            RequestingUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value)
        };

        var result = await _createPatientHandler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to create patient: {Error}", result.Error);
            return BadRequest(ApiResponse<int>.ErrorResponse(result.Error, "Failed to create patient"));
        }

        _logger.LogInformation("Patient {PatientId} created successfully", result.Value);
        return CreatedAtAction(
            nameof(GetPatientById),
            new { id = result.Value },
            ApiResponse<int>.SuccessResponse(result.Value, "Patient created successfully"));
    }

    /// <summary>
    /// Gets a patient by ID.
    /// </summary>
    /// <param name="id">The patient ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The patient details.</returns>
    /// <response code="200">Patient found.</response>
    /// <response code="403">Forbidden for non-owners.</response>
    /// <response code="404">Patient not found.</response>
    [HttpGet("{id}")]
    [Authorize(Roles = AppRoles.PatientOrDoctorOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse<PatientDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPatientById(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving patient {PatientId}", id);

        var role = User.GetRole();
        int? accessorId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null;

        // Patient: own only. Doctor: care relationship required. Admin: unrestricted.
        var denyReason = await PatientRecordAccess.GetDenyReasonForPatientDataAsync(
            User, id, _unitOfWork.Appointments, cancellationToken);
        if (denyReason is not null)
        {
            await _auditLogService.WriteAsync(
                AuditActions.GetPatientById,
                "Patient",
                id,
                AuditOutcome.Failure,
                details: new { Reason = denyReason },
                actorUserIdOverride: accessorId,
                actorRoleOverride: role,
                cancellationToken: cancellationToken);
            return Forbid();
        }

        var patient = await _unitOfWork.Patients.GetByIdAsync(id, cancellationToken);

        if (patient == null)
        {
            _logger.LogWarning("Patient {PatientId} not found", id);
            await _auditLogService.WriteAsync(
                AuditActions.GetPatientById,
                "Patient",
                id,
                AuditOutcome.Failure,
                details: new { Reason = "not_found" },
                actorUserIdOverride: accessorId,
                actorRoleOverride: role,
                cancellationToken: cancellationToken);
            return NotFound(ApiResponse<PatientDto>.ErrorResponse(
                _localizer["PatientNotFoundWithId", id],
                _localizer["PatientNotFound"]));
        }

        var dto = MapToDto(patient);

        // Immutable PHI access audit (all roles including self-access for compliance trail).
        await _auditLogService.WriteAsync(
            AuditActions.GetPatientById,
            "Patient",
            id,
            AuditOutcome.Success,
            details: new { Via = "GetPatientById" },
            actorUserIdOverride: accessorId,
            actorRoleOverride: role,
            cancellationToken: cancellationToken);

        // Keep domain event for existing outbox / secondary handlers (Doctor/Admin only to reduce noise).
        if (role != AppRoles.Patient)
        {
            await _eventDispatcher.DispatchAsync(new PatientRecordAccessedEvent(
                id,
                accessorId,
                "Patient profile viewed via GetPatientById API"), cancellationToken);
        }

        return Ok(ApiResponse<PatientDto>.SuccessResponse(dto));
    }

    /// <summary>
    /// Gets paginated list of all patients.
    /// </summary>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="pageSize">Items per page (default: 20, max: 100)</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of patients.</returns>
    /// <response code="200">Patients retrieved successfully.</response>
    [HttpGet]
    [Authorize(Roles = AppRoles.DoctorOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PatientDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllPatients(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving patients - Page: {Page}, Size: {Size}", pageNumber, pageSize);

        // Validate pagination
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var pagedEntities = await _unitOfWork.Patients
            .GetPagedAsync(pageNumber, pageSize, cancellationToken);
        var pagedResult = new PagedResult<PatientDto>(
            pagedEntities.Items.Select(MapToDto),
            pagedEntities.PageNumber,
            pagedEntities.PageSize,
            pagedEntities.TotalCount);

        return Ok(ApiResponse<PagedResult<PatientDto>>.SuccessResponse(
            pagedResult,
            $"Retrieved page {pageNumber} of {pagedResult.TotalPages} ({pagedResult.Items.Count()} items)"));
    }

    /// <summary>
    /// Gets paginated list of all active patients.
    /// </summary>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="pageSize">Items per page (default: 20, max: 100)</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of active patients.</returns>
    /// <response code="200">Active patients retrieved successfully.</response>
    [HttpGet("active")]
    [Authorize(Roles = AppRoles.DoctorOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PatientDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetActivePatients(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving active patients - Page: {Page}, Size: {Size}", pageNumber, pageSize);

        // Validate pagination
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var pagedEntities = await _unitOfWork.Patients
            .GetPagedActiveAsync(pageNumber, pageSize, cancellationToken);
        var pagedResult = new PagedResult<PatientDto>(
            pagedEntities.Items.Select(MapToDto),
            pagedEntities.PageNumber,
            pagedEntities.PageSize,
            pagedEntities.TotalCount);

        return Ok(ApiResponse<PagedResult<PatientDto>>.SuccessResponse(
            pagedResult,
            $"Retrieved page {pageNumber} of {pagedResult.TotalPages} ({pagedResult.Items.Count()} active patient(s))"));
    }

    /// <summary>
    /// Searches patients by name with pagination.
    /// </summary>
    /// <param name="term">The search term.</param>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="pageSize">Items per page (default: 20, max: 100)</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of matching patients.</returns>
    /// <response code="200">Search completed successfully.</response>
    /// <response code="400">Search term is required.</response>
    [HttpGet("search")]
    [Authorize(Roles = AppRoles.DoctorOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PatientDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SearchPatients(
        [FromQuery] string term,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return BadRequest(ApiResponse<PagedResult<PatientDto>>.ErrorResponse(
                "Search term is required",
                "Invalid search"));
        }

        _logger.LogInformation("Searching patients with term: {Term} - Page: {Page}, Size: {Size}", term, pageNumber, pageSize);

        // Validate pagination
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var pagedEntities = await _unitOfWork.Patients
            .GetPagedSearchByNameAsync(term, pageNumber, pageSize, cancellationToken);
        var pagedResult = new PagedResult<PatientDto>(
            pagedEntities.Items.Select(MapToDto),
            pagedEntities.PageNumber,
            pagedEntities.PageSize,
            pagedEntities.TotalCount);

        return Ok(ApiResponse<PagedResult<PatientDto>>.SuccessResponse(
            pagedResult,
            $"Found {pagedResult.TotalCount} patient(s) matching '{term}' - page {pageNumber} of {pagedResult.TotalPages}"));
    }

    /// <summary>
    /// Updates the patient's notification preferences.
    /// </summary>
    [HttpPut("{id}/notification-preferences")]
    [Authorize(Roles = AppRoles.PatientOrDoctorOrAdmin)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateNotificationPreferences(
        int id,
        [FromBody] UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating notification preferences for patient {PatientId}", id);

        var role = User.GetRole();
        if (role == AppRoles.Patient && User.GetPatientId() != id)
            return Forbid();

        var patient = await _unitOfWork.Patients.GetByIdAsync(id, cancellationToken);
        if (patient == null)
        {
            _logger.LogWarning("Patient {PatientId} not found", id);
            return NotFound(ApiResponse.ErrorResponse(
                _localizer["PatientNotFoundWithId", id], _localizer["PatientNotFound"]));
        }

        patient.UpdateNotificationPreferences(request.EmailEnabled, request.SmsEnabled);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Notification preferences updated for patient {PatientId}", id);
        return Ok(ApiResponse.SuccessResponse(_localizer["NotificationPreferencesUpdated"]));
    }

    /// <summary>
    /// Deactivates a patient (soft-delete). The record remains in the database
    /// but IsActive is set to false, preserving the historical/audit trail.
    /// </summary>
    /// <param name="id">The patient ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success or failure result.</returns>
    /// <response code="204">Patient deactivated successfully.</response>
    /// <response code="400">Patient is already deactivated.</response>
    /// <response code="403">Forbidden — Admin only.</response>
    /// <response code="404">Patient not found.</response>
    [HttpDelete("{id}")]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePatient(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deactivating patient {PatientId}", id);

        var patient = await _unitOfWork.Patients.GetByIdAsync(id, cancellationToken);
        if (patient == null)
        {
            _logger.LogWarning("Patient {PatientId} not found", id);
            return NotFound(ApiResponse.ErrorResponse(
                _localizer["PatientNotFoundWithId", id],
                _localizer["PatientNotFound"]));
        }

        try
        {
            patient.Deactivate();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Patient {PatientId} is already deactivated", id);
            return BadRequest(ApiResponse.ErrorResponse(ex.Message, "Patient already deactivated"));
        }

        await _unitOfWork.Patients.UpdateAsync(patient, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Patient {PatientId} deactivated successfully", id);
        return NoContent();
    }

    /// <summary>
    /// Anonymizes a patient record — irreversibly replaces all PII while
    /// preserving the primary key for foreign-key integrity.
    /// </summary>
    /// <param name="id">The patient ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Patient anonymized successfully.</response>
    /// <response code="400">Patient is already anonymized.</response>
    /// <response code="403">Forbidden — Admin only.</response>
    /// <response code="404">Patient not found.</response>
    [HttpPost("{id}/anonymize")]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AnonymizePatient(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Anonymizing patient {PatientId}", id);

        var command = new AnonymizePatientCommand { PatientId = id };
        var result = await _anonymizePatientHandler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Anonymization failed for patient {PatientId}: {Error}", id, result.Error);

            return result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(ApiResponse.ErrorResponse(result.Error, "Patient not found"))
                : BadRequest(ApiResponse.ErrorResponse(result.Error, "Anonymization failed"));
        }

        _logger.LogInformation("Patient {PatientId} anonymized successfully", id);
        return Ok(ApiResponse.SuccessResponse("Patient data anonymized successfully."));
    }

    /// <summary>
    /// Maps Patient entity to PatientDto.
    /// </summary>
    private static PatientDto MapToDto(Domain.Entities.Patient patient)
    {
        return new PatientDto
        {
            Id = patient.Id,
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            FullName = patient.FullName,
            Email = patient.Email.Value,
            PhoneNumber = patient.PhoneNumber.Value,
            DateOfBirth = patient.DateOfBirth,
            Age = patient.Age,
            Gender = patient.Gender.ToString(),
            Address = patient.Address.GetFullAddress(),
            IsActive = patient.IsActive,
            IsAnonymized = patient.IsAnonymized,
            EmailEnabled = patient.NotificationPreferences.EmailEnabled,
            SmsEnabled = patient.NotificationPreferences.SmsEnabled,
            CreatedAt = patient.CreatedAt
        };
    }
}
