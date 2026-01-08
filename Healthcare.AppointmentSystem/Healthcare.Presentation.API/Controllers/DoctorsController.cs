using Asp.Versioning;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Healthcare.Presentation.API.Requests;
using Healthcare.Presentation.API.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Healthcare.Presentation.API.Controllers;

/// <summary>
/// Controller for managing doctors.
/// </summary>
/// <remarks>
/// REST Endpoints:
/// - POST   /api/doctors          - Create new doctor
/// - GET    /api/doctors/{id}     - Get doctor by ID
/// - GET    /api/doctors          - Get all doctors
/// - GET    /api/doctors/active   - Get active doctors
/// - GET    /api/doctors/accepting-patients - Get doctors accepting patients
/// - DELETE /api/doctors/{id}     - Delete doctor
/// </remarks>
[ApiController]
[ApiVersion("1.0")] 
[Route("api/v{version:apiVersion}/[controller]")] // ← NDRYSHO KËTË
[Produces("application/json")]
public sealed class DoctorsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DoctorsController> _logger;

    public DoctorsController(
        IUnitOfWork unitOfWork,
        ILogger<DoctorsController> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new doctor.
    /// </summary>
    /// <param name="request">The doctor details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ID of the created doctor.</returns>
    /// <response code="201">Doctor created successfully.</response>
    /// <response code="400">Invalid request data or doctor already exists.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateDoctor(
        [FromBody] CreateDoctorRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating doctor: {Email}", request.Email);

        try
        {
            // Check if doctor already exists
            var existingDoctor = await _unitOfWork.Doctors
                .GetByEmailAsync(request.Email, cancellationToken);

            if (existingDoctor != null)
            {
                return BadRequest(ApiResponse<int>.ErrorResponse(
                    $"A doctor with email '{request.Email}' already exists",
                    "Doctor already exists"));
            }

            // Create value objects
            var email = Email.Create(request.Email);
            var phoneNumber = PhoneNumber.Create(request.PhoneNumber);
            var consultationFee = Money.Create(
                request.ConsultationFeeAmount,
                request.ConsultationFeeCurrency);

            // Parse specialty
            if (!Enum.TryParse<Specialty>(request.Specialty, true, out var specialty))
            {
                return BadRequest(ApiResponse<int>.ErrorResponse(
                    $"Invalid specialty: {request.Specialty}",
                    "Invalid specialty"));
            }

            // Create doctor entity
            var doctor = Doctor.Create(
                request.FirstName,
                request.LastName,
                email,
                phoneNumber,
                request.LicenseNumber,
                consultationFee,
                request.YearsOfExperience,
                specialty);

            // Persist
            await _unitOfWork.Doctors.AddAsync(doctor, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Doctor {DoctorId} created successfully", doctor.Id);
            return CreatedAtAction(
                nameof(GetDoctorById),
                new { id = doctor.Id },
                ApiResponse<int>.SuccessResponse(doctor.Id, "Doctor created successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create doctor");
            return BadRequest(ApiResponse<int>.ErrorResponse(ex.Message, "Failed to create doctor"));
        }
    }

    /// <summary>
    /// Gets a doctor by ID.
    /// </summary>
    /// <param name="id">The doctor ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The doctor details.</returns>
    /// <response code="200">Doctor found.</response>
    /// <response code="404">Doctor not found.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<DoctorDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDoctorById(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving doctor {DoctorId}", id);

        var doctor = await _unitOfWork.Doctors.GetByIdAsync(id, cancellationToken);

        if (doctor == null)
        {
            _logger.LogWarning("Doctor {DoctorId} not found", id);
            return NotFound(ApiResponse<DoctorDto>.ErrorResponse(
                $"Doctor with ID {id} not found",
                "Doctor not found"));
        }

        var dto = MapToDto(doctor);
        return Ok(ApiResponse<DoctorDto>.SuccessResponse(dto));
    }

    /// <summary>
    /// Gets all doctors.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all doctors.</returns>
    /// <response code="200">Doctors retrieved successfully.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<DoctorDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllDoctors(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving all doctors");

        var doctors = await _unitOfWork.Doctors.GetAllAsync(cancellationToken);
        var dtos = doctors.Select(MapToDto).ToList();

        return Ok(ApiResponse<List<DoctorDto>>.SuccessResponse(
            dtos,
            $"Retrieved {dtos.Count} doctor(s)"));
    }

    /// <summary>
    /// Gets all active doctors.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active doctors.</returns>
    /// <response code="200">Active doctors retrieved successfully.</response>
    [HttpGet("active")]
    [ProducesResponseType(typeof(ApiResponse<List<DoctorDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveDoctors(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving active doctors");

        var doctors = await _unitOfWork.Doctors.GetActiveAsync(cancellationToken);
        var dtos = doctors.Select(MapToDto).ToList();

        return Ok(ApiResponse<List<DoctorDto>>.SuccessResponse(
            dtos,
            $"Retrieved {dtos.Count} active doctor(s)"));
    }

    /// <summary>
    /// Gets doctors accepting new patients.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of doctors accepting patients.</returns>
    /// <response code="200">Doctors retrieved successfully.</response>
    [HttpGet("accepting-patients")]
    [ProducesResponseType(typeof(ApiResponse<List<DoctorDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDoctorsAcceptingPatients(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving doctors accepting patients");

        var doctors = await _unitOfWork.Doctors.GetAcceptingPatientsAsync(cancellationToken);
        var dtos = doctors.Select(MapToDto).ToList();

        return Ok(ApiResponse<List<DoctorDto>>.SuccessResponse(
            dtos,
            $"Retrieved {dtos.Count} doctor(s) accepting patients"));
    }

    /// <summary>
    /// Deletes a doctor.
    /// </summary>
    /// <param name="id">The doctor ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success or failure result.</returns>
    /// <response code="204">Doctor deleted successfully.</response>
    /// <response code="404">Doctor not found.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDoctor(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting doctor {DoctorId}", id);

        var doctor = await _unitOfWork.Doctors.GetByIdAsync(id, cancellationToken);
        if (doctor == null)
        {
            _logger.LogWarning("Doctor {DoctorId} not found", id);
            return NotFound(ApiResponse.ErrorResponse(
                $"Doctor with ID {id} not found",
                "Doctor not found"));
        }

        await _unitOfWork.Doctors.DeleteAsync(id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Doctor {DoctorId} deleted successfully", id);
        return NoContent();
    }

    /// <summary>
    /// Maps Doctor entity to DoctorDto.
    /// </summary>
    private static DoctorDto MapToDto(Doctor doctor)
    {
        return new DoctorDto
        {
            Id = doctor.Id,
            FirstName = doctor.FirstName,
            LastName = doctor.LastName,
            FullName = doctor.FullName,
            Email = doctor.Email.Value,
            PhoneNumber = doctor.PhoneNumber.Value,
            LicenseNumber = doctor.LicenseNumber,
            Specialties = doctor.Specialties.Select(s => s.ToString()).ToList(),
            ConsultationFeeAmount = doctor.ConsultationFee.Amount,
            ConsultationFeeCurrency = doctor.ConsultationFee.Currency,
            IsAcceptingPatients = doctor.IsAcceptingPatients,
            IsActive = doctor.IsActive,
            YearsOfExperience = doctor.YearsOfExperience,
            CreatedAt = doctor.CreatedAt
        };
    }
}