using Asp.Versioning;
using Healthcare.Application.Commands.CreateDoctor;
using Healthcare.Application.Commands.DeactivateDoctor;
using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Caching;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Presentation.API.Requests;
using Healthcare.Presentation.API.Resources;
using Healthcare.Presentation.API.Authorization;
using Healthcare.Presentation.API.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Security.Claims;

namespace Healthcare.Presentation.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public sealed class DoctorsController : ControllerBase
{
    private readonly ICommandHandler<CreateDoctorCommand, Result<int>> _createDoctorHandler;
    private readonly ICommandHandler<DeactivateDoctorCommand, Result> _deactivateDoctorHandler;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDoctorCacheService _cache;
    private readonly IStringLocalizer<Messages> _localizer;
    private readonly ILogger<DoctorsController> _logger;

    public DoctorsController(
        ICommandHandler<CreateDoctorCommand, Result<int>> createDoctorHandler,
        ICommandHandler<DeactivateDoctorCommand, Result> deactivateDoctorHandler,
        IUnitOfWork unitOfWork,
        IDoctorCacheService cache,
        IStringLocalizer<Messages> localizer,
        ILogger<DoctorsController> logger)
    {
        _createDoctorHandler = createDoctorHandler;
        _deactivateDoctorHandler = deactivateDoctorHandler;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.AdminOrDoctor)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateDoctor(
        [FromBody] CreateDoctorRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating doctor: {Email}", request.Email);

        var command = new CreateDoctorCommand
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            LicenseNumber = request.LicenseNumber,
            Specialty = request.Specialty,
            ConsultationFeeAmount = request.ConsultationFeeAmount,
            ConsultationFeeCurrency = request.ConsultationFeeCurrency,
            YearsOfExperience = request.YearsOfExperience,
            RequestingUserId = User.IsInRole(AppRoles.Doctor) ? int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value) : null
        };

        var result = await _createDoctorHandler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to create doctor: {Error}", result.Error);
            return BadRequest(ApiResponse<int>.ErrorResponse(result.Error, "Doctor already exists"));
        }

        _logger.LogInformation("Doctor {DoctorId} created successfully", result.Value);
        return CreatedAtAction(
            nameof(GetDoctorById),
            new { id = result.Value },
            ApiResponse<int>.SuccessResponse(result.Value, "Doctor created successfully"));
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
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

    [HttpDelete("{id}")]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteDoctor(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deactivating doctor {DoctorId}", id);

        var command = new DeactivateDoctorCommand { DoctorId = id };
        var result = await _deactivateDoctorHandler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to deactivate doctor {DoctorId}: {Error}", id, result.Error);
            return BadRequest(ApiResponse.ErrorResponse(result.Error, "Doctor already deactivated"));
        }

        _logger.LogInformation("Doctor {DoctorId} deactivated successfully", id);
        return NoContent();
    }

    [HttpGet]
    [AllowAnonymous]
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
    [AllowAnonymous]
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
    [AllowAnonymous]
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
