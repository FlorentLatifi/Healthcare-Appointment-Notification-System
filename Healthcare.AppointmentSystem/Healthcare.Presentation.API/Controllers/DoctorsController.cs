using Asp.Versioning;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Caching;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.Events;
using Healthcare.Domain.ValueObjects;
using Healthcare.Presentation.API.Requests;
using Healthcare.Presentation.API.Responses;
using Microsoft.AspNetCore.Mvc;
using Healthcare.Application.Common;
using Healthcare.Presentation.API.Resources;
using Microsoft.Extensions.Localization;

namespace Healthcare.Presentation.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public sealed class DoctorsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDoctorCacheService _cache;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly IStringLocalizer<Messages> _localizer;
    private readonly ILogger<DoctorsController> _logger;

    public DoctorsController(
        IUnitOfWork unitOfWork,
        IDoctorCacheService cache,
        IDomainEventDispatcher eventDispatcher,
        IStringLocalizer<Messages> localizer,
        ILogger<DoctorsController> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _eventDispatcher = eventDispatcher;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateDoctor(
        [FromBody] CreateDoctorRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating doctor: {Email}", request.Email);

        var existingDoctor = await _unitOfWork.Doctors
                .GetByEmailAsync(request.Email, cancellationToken);

            if (existingDoctor != null)
            {
                return BadRequest(ApiResponse<int>.ErrorResponse(
                    $"A doctor with email '{request.Email}' already exists",
                    "Doctor already exists"));
            }

            var email = Email.Create(request.Email);
            var phoneNumber = PhoneNumber.Create(request.PhoneNumber);
            var consultationFee = Money.Create(
                request.ConsultationFeeAmount,
                request.ConsultationFeeCurrency);

            if (!Enum.TryParse<Specialty>(request.Specialty, true, out var specialty))
            {
                return BadRequest(ApiResponse<int>.ErrorResponse(
                    $"Invalid specialty: {request.Specialty}",
                    "Invalid specialty"));
            }

            var doctor = Doctor.Create(
                request.FirstName,
                request.LastName,
                email,
                phoneNumber,
                request.LicenseNumber,
                consultationFee,
                request.YearsOfExperience,
                specialty);

            await _unitOfWork.Doctors.AddAsync(doctor, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _eventDispatcher.DispatchAsync(
                new DoctorCacheInvalidationNeededEvent(), cancellationToken);

            _logger.LogInformation("Doctor {DoctorId} created successfully", doctor.Id);
            return CreatedAtAction(
                nameof(GetDoctorById),
                new { id = doctor.Id },
                ApiResponse<int>.SuccessResponse(doctor.Id, "Doctor created successfully"));
    }

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
                _localizer["DoctorNotFoundWithId", id],
                _localizer["DoctorNotFound"]));
        }

        var dto = MapToDto(doctor);
        return Ok(ApiResponse<DoctorDto>.SuccessResponse(dto));
    }

    /// <summary>
    /// Deactivates a doctor (soft-delete). The record remains in the database
    /// but IsActive is set to false (and IsAcceptingPatients to false),
    /// preserving the historical/audit trail.
    /// </summary>
    /// <param name="id">The doctor ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success or failure result.</returns>
    /// <response code="204">Doctor deactivated successfully.</response>
    /// <response code="400">Doctor is already deactivated.</response>
    /// <response code="404">Doctor not found.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDoctor(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deactivating doctor {DoctorId}", id);

        var doctor = await _unitOfWork.Doctors.GetByIdAsync(id, cancellationToken);
        if (doctor == null)
        {
            _logger.LogWarning("Doctor {DoctorId} not found", id);
            return NotFound(ApiResponse.ErrorResponse(
                _localizer["DoctorNotFoundWithId", id],
                _localizer["DoctorNotFound"]));
        }

        try
        {
            doctor.Deactivate();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Doctor {DoctorId} is already deactivated", id);
            return BadRequest(ApiResponse.ErrorResponse(ex.Message, "Doctor already deactivated"));
        }

        await _unitOfWork.Doctors.UpdateAsync(doctor, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _eventDispatcher.DispatchAsync(
            new DoctorCacheInvalidationNeededEvent(), cancellationToken);

        _logger.LogInformation("Doctor {DoctorId} deactivated successfully", id);
        return NoContent();
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DoctorDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllDoctors(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving doctors - Page: {Page}, Size: {Size}", pageNumber, pageSize);

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var cached = await _cache.GetAsync("all", cancellationToken);
        IReadOnlyList<DoctorDto> dtos;
        if (cached != null)
        {
            dtos = cached;
        }
        else
        {
            var doctors = await _unitOfWork.Doctors.GetAllAsync(cancellationToken);
            dtos = doctors.Select(MapToDto).ToList();
            await _cache.SetAsync("all", dtos, cancellationToken);
        }

        var pagedResult = PagedResult<DoctorDto>.Create(dtos, pageNumber, pageSize);

        return Ok(ApiResponse<PagedResult<DoctorDto>>.SuccessResponse(
            pagedResult,
            $"Retrieved page {pageNumber} of {pagedResult.TotalPages} ({pagedResult.Items.Count()} items)"));
    }

    [HttpGet("active")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DoctorDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveDoctors(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving active doctors - Page: {Page}, Size: {Size}", pageNumber, pageSize);

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var cached = await _cache.GetAsync("active", cancellationToken);
        IReadOnlyList<DoctorDto> dtos;
        if (cached != null)
        {
            dtos = cached;
        }
        else
        {
            var doctors = await _unitOfWork.Doctors.GetActiveAsync(cancellationToken);
            dtos = doctors.Select(MapToDto).ToList();
            await _cache.SetAsync("active", dtos, cancellationToken);
        }

        var pagedResult = PagedResult<DoctorDto>.Create(dtos, pageNumber, pageSize);

        return Ok(ApiResponse<PagedResult<DoctorDto>>.SuccessResponse(
            pagedResult,
            $"Retrieved page {pageNumber} of {pagedResult.TotalPages} ({pagedResult.Items.Count()} active doctor(s))"));
    }

    [HttpGet("accepting-patients")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DoctorDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDoctorsAcceptingPatients(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving doctors accepting patients - Page: {Page}, Size: {Size}", pageNumber, pageSize);

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var cached = await _cache.GetAsync("accepting-patients", cancellationToken);
        IReadOnlyList<DoctorDto> dtos;
        if (cached != null)
        {
            dtos = cached;
        }
        else
        {
            var doctors = await _unitOfWork.Doctors.GetAcceptingPatientsAsync(cancellationToken);
            dtos = doctors.Select(MapToDto).ToList();
            await _cache.SetAsync("accepting-patients", dtos, cancellationToken);
        }

        var pagedResult = PagedResult<DoctorDto>.Create(dtos, pageNumber, pageSize);

        return Ok(ApiResponse<PagedResult<DoctorDto>>.SuccessResponse(
            pagedResult,
            $"Retrieved page {pageNumber} of {pagedResult.TotalPages} ({pagedResult.Items.Count()} doctor(s) accepting patients)"));
    }

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
