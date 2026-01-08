using Asp.Versioning;
using Healthcare.Application.Commands.CreatePatient;
using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Presentation.API.Requests;
using Healthcare.Presentation.API.Responses;
using Microsoft.AspNetCore.Mvc;


namespace Healthcare.Presentation.API.Controllers;

/// <summary>
/// Controller for managing patients.
/// </summary>
/// <remarks>
/// REST Endpoints:
/// - POST   /api/patients          - Create new patient
/// - GET    /api/patients/{id}     - Get patient by ID
/// - GET    /api/patients          - Get all patients
/// - GET    /api/patients/active   - Get active patients
/// - GET    /api/patients/search?term={term} - Search by name
/// - DELETE /api/patients/{id}     - Delete patient
/// </remarks>
[ApiController]
[ApiVersion("1.0")] // ← SHTO KËTË
[Route("api/v{version:apiVersion}/[controller]")] // ← NDRYSHO KËTË
[Produces("application/json")]
public sealed class PatientsController : ControllerBase
{
    private readonly ICommandHandler<CreatePatientCommand, Result<int>> _createPatientHandler;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PatientsController> _logger;

    public PatientsController(
        ICommandHandler<CreatePatientCommand, Result<int>> createPatientHandler,
        IUnitOfWork unitOfWork,
        ILogger<PatientsController> logger)
    {
        _createPatientHandler = createPatientHandler;
        _unitOfWork = unitOfWork;
        _logger = logger;
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
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
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
            Country = request.Country
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
    /// <response code="404">Patient not found.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<PatientDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPatientById(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving patient {PatientId}", id);

        var patient = await _unitOfWork.Patients.GetByIdAsync(id, cancellationToken);

        if (patient == null)
        {
            _logger.LogWarning("Patient {PatientId} not found", id);
            return NotFound(ApiResponse<PatientDto>.ErrorResponse(
                $"Patient with ID {id} not found",
                "Patient not found"));
        }

        var dto = MapToDto(patient);
        return Ok(ApiResponse<PatientDto>.SuccessResponse(dto));
    }

    /// <summary>
    /// Gets all patients.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all patients.</returns>
    /// <response code="200">Patients retrieved successfully.</response>
    /// <summary>
    /// Gets paginated list of all patients.
    /// </summary>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="pageSize">Items per page (default: 20, max: 100)</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of patients.</returns>
    /// <response code="200">Patients retrieved successfully.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PatientDto>>), StatusCodes.Status200OK)]
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

        var patients = await _unitOfWork.Patients.GetAllAsync(cancellationToken);
        var dtos = patients.Select(MapToDto);

        var pagedResult = PagedResult<PatientDto>.Create(dtos, pageNumber, pageSize);

        return Ok(ApiResponse<PagedResult<PatientDto>>.SuccessResponse(
            pagedResult,
            $"Retrieved page {pageNumber} of {pagedResult.TotalPages} ({pagedResult.Items.Count()} items)"));
    }

    /// <summary>
    /// Gets all active patients.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active patients.</returns>
    /// <response code="200">Active patients retrieved successfully.</response>
    [HttpGet("active")]
    [ProducesResponseType(typeof(ApiResponse<List<PatientDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivePatients(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving active patients");

        var patients = await _unitOfWork.Patients.GetActiveAsync(cancellationToken);
        var dtos = patients.Select(MapToDto).ToList();

        return Ok(ApiResponse<List<PatientDto>>.SuccessResponse(
            dtos,
            $"Retrieved {dtos.Count} active patient(s)"));
    }

    /// <summary>
    /// Searches patients by name.
    /// </summary>
    /// <param name="term">The search term.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching patients.</returns>
    /// <response code="200">Search completed successfully.</response>
    /// <response code="400">Search term is required.</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponse<List<PatientDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchPatients(
        [FromQuery] string term,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return BadRequest(ApiResponse<List<PatientDto>>.ErrorResponse(
                "Search term is required",
                "Invalid search"));
        }

        _logger.LogInformation("Searching patients with term: {Term}", term);

        var patients = await _unitOfWork.Patients.SearchByNameAsync(term, cancellationToken);
        var dtos = patients.Select(MapToDto).ToList();

        return Ok(ApiResponse<List<PatientDto>>.SuccessResponse(
            dtos,
            $"Found {dtos.Count} patient(s) matching '{term}'"));
    }

    /// <summary>
    /// Deletes a patient.
    /// </summary>
    /// <param name="id">The patient ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success or failure result.</returns>
    /// <response code="204">Patient deleted successfully.</response>
    /// <response code="404">Patient not found.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePatient(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting patient {PatientId}", id);

        var patient = await _unitOfWork.Patients.GetByIdAsync(id, cancellationToken);
        if (patient == null)
        {
            _logger.LogWarning("Patient {PatientId} not found", id);
            return NotFound(ApiResponse.ErrorResponse(
                $"Patient with ID {id} not found",
                "Patient not found"));
        }

        await _unitOfWork.Patients.DeleteAsync(id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Patient {PatientId} deleted successfully", id);
        return NoContent();
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
            CreatedAt = patient.CreatedAt
        };
    }
}