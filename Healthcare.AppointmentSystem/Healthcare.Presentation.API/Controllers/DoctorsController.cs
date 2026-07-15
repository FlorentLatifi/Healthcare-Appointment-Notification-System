using Asp.Versioning;
using Healthcare.Application.Commands.CreateDoctor;
using Healthcare.Application.Commands.DeactivateDoctor;
using Healthcare.Application.Commands.UpdateDoctor;
using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Authentication;
using Healthcare.Application.Ports.Caching;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
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
    private readonly ICommandHandler<UpdateDoctorCommand, Result> _updateDoctorHandler;
    private readonly ICommandHandler<DeactivateDoctorCommand, Result> _deactivateDoctorHandler;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDoctorCacheService _doctorCache;
    private readonly IAvailabilityCacheService _availabilityCache;
    private readonly IStringLocalizer<Messages> _localizer;
    private readonly ILogger<DoctorsController> _logger;
    private readonly IAuthenticationService _authService;

    public DoctorsController(
        ICommandHandler<CreateDoctorCommand, Result<int>> createDoctorHandler,
        ICommandHandler<UpdateDoctorCommand, Result> updateDoctorHandler,
        ICommandHandler<DeactivateDoctorCommand, Result> deactivateDoctorHandler,
        IUnitOfWork unitOfWork,
        IDoctorCacheService doctorCache,
        IAvailabilityCacheService availabilityCache,
        IStringLocalizer<Messages> localizer,
        ILogger<DoctorsController> logger,
        IAuthenticationService authService)
    {
        _createDoctorHandler = createDoctorHandler;
        _updateDoctorHandler = updateDoctorHandler;
        _deactivateDoctorHandler = deactivateDoctorHandler;
        _unitOfWork = unitOfWork;
        _doctorCache = doctorCache;
        _availabilityCache = availabilityCache;
        _localizer = localizer;
        _logger = logger;
        _authService = authService;
    }

    /// <summary>
    /// Creates a doctor profile. When the caller is a Doctor, the profile is linked to their
    /// account and the response includes a re-issued access token with <c>doctor_id</c>.
    /// Admin catalog creates return the profile id without a session token.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = AppRoles.AdminOrDoctor)]
    [ProducesResponseType(typeof(ApiResponse<ProfileCreatedResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateDoctor(
        [FromBody] CreateDoctorRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating doctor: {Email}", request.Email);

        int? requestingUserId = User.IsInRole(AppRoles.Doctor)
            ? int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value)
            : null;

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
            RequestingUserId = requestingUserId
        };

        var result = await _createDoctorHandler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to create doctor: {Error}", result.Error);
            return BadRequest(ApiResponse<ProfileCreatedResponse>.ErrorResponse(
                result.Error, "Doctor already exists"));
        }

        _logger.LogInformation("Doctor {DoctorId} created successfully", result.Value);

        var payload = new ProfileCreatedResponse { Id = result.Value };

        // Self-service doctor: re-issue JWT with doctor_id so the SPA skips /Auth/refresh.
        if (requestingUserId is int userId)
        {
            var session = await _authService.IssueAccessTokenForUserAsync(userId, cancellationToken);
            if (session.IsSuccess)
            {
                payload.Token = session.Value.AccessToken;
                payload.ExpiresAt = session.Value.ExpiresAt;
                payload.Username = session.Value.Username;
                payload.Role = session.Value.Role;
                payload.PatientId = session.Value.PatientId;
                payload.DoctorId = session.Value.DoctorId;
            }
            else
            {
                _logger.LogWarning(
                    "Doctor {DoctorId} created but could not re-issue access token for user {UserId}: {Error}",
                    result.Value, userId, session.Error);
            }
        }

        return CreatedAtAction(
            nameof(GetDoctorById),
            new { id = result.Value },
            ApiResponse<ProfileCreatedResponse>.SuccessResponse(
                payload, "Doctor created successfully"));
    }

    /// <summary>
    /// Cache-aside: doctor by id (catalog TTL). Misses load from DB with stampede protection.
    /// </summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<DoctorDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDoctorById(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving doctor {DoctorId}", id);

        var dto = await _doctorCache.GetDoctorByIdAsync(
            id,
            async ct =>
            {
                var doctor = await _unitOfWork.Doctors.GetByIdAsync(id, ct);
                return doctor is null ? null : MapToDto(doctor);
            },
            cancellationToken);

        if (dto is null)
        {
            _logger.LogWarning("Doctor {DoctorId} not found", id);
            return NotFound(ApiResponse<DoctorDto>.ErrorResponse(
                _localizer["DoctorNotFoundWithId", id],
                _localizer["DoctorNotFound"]));
        }

        return Ok(ApiResponse<DoctorDto>.SuccessResponse(dto));
    }

    /// <summary>
    /// Weekly schedule (working hours) — longer TTL; invalidated with doctor catalog events.
    /// </summary>
    [HttpGet("{id:int}/schedule")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<DoctorScheduleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDoctorSchedule(int id, CancellationToken cancellationToken)
    {
        var schedule = await _doctorCache.GetScheduleAsync(
            id,
            async ct =>
            {
                var doctor = await _unitOfWork.Doctors.GetByIdAsync(id, ct);
                return doctor is null ? null : MapToScheduleDto(doctor);
            },
            cancellationToken);

        if (schedule is null)
        {
            return NotFound(ApiResponse<DoctorScheduleDto>.ErrorResponse(
                _localizer["DoctorNotFoundWithId", id],
                _localizer["DoctorNotFound"]));
        }

        return Ok(ApiResponse<DoctorScheduleDto>.SuccessResponse(schedule));
    }

    /// <summary>
    /// Day-level booked slots (short TTL). Not authoritative for booking — writes always re-check DB under lock.
    /// </summary>
    [HttpGet("{id:int}/availability")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<DoctorDayAvailabilityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDoctorAvailability(
        int id,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var day = date ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var doctorExists = await _doctorCache.GetDoctorByIdAsync(
            id,
            async ct =>
            {
                var doctor = await _unitOfWork.Doctors.GetByIdAsync(id, ct);
                return doctor is null ? null : MapToDto(doctor);
            },
            cancellationToken);

        if (doctorExists is null)
        {
            return NotFound(ApiResponse<DoctorDayAvailabilityDto>.ErrorResponse(
                _localizer["DoctorNotFoundWithId", id],
                _localizer["DoctorNotFound"]));
        }

        var availability = await _availabilityCache.GetDayAsync(
            id,
            day,
            async ct =>
            {
                var appointments = await _unitOfWork.Appointments.GetByDoctorAndDateAsync(
                    id, day.ToDateTime(TimeOnly.MinValue), ct);

                return new DoctorDayAvailabilityDto
                {
                    DoctorId = id,
                    Date = day,
                    CachedAtUtc = DateTime.UtcNow,
                    BookedSlots = appointments
                        .Where(a => a.Status is not AppointmentStatus.Cancelled and not AppointmentStatus.NoShow)
                        .Select(a => new BookedSlotDto
                        {
                            AppointmentId = a.Id,
                            StartUtc = a.ScheduledTime.Value,
                            Status = a.Status.ToString()
                        })
                        .OrderBy(s => s.StartUtc)
                        .ToList()
                };
            },
            cancellationToken);

        return Ok(ApiResponse<DoctorDayAvailabilityDto>.SuccessResponse(availability!));
    }

    /// <summary>
    /// Updates an existing doctor profile (owner or admin).
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.AdminOrDoctor)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDoctor(
        int id,
        [FromBody] UpdateDoctorRequest request,
        CancellationToken cancellationToken)
    {
        if (User.IsInRole(AppRoles.Doctor) && User.GetDoctorId() != id)
            return Forbid();

        _logger.LogInformation("Updating doctor {DoctorId}", id);

        var command = new UpdateDoctorCommand
        {
            DoctorId = id,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            LicenseNumber = request.LicenseNumber,
            Specialty = request.Specialty,
            ConsultationFeeAmount = request.ConsultationFeeAmount,
            ConsultationFeeCurrency = request.ConsultationFeeCurrency,
            YearsOfExperience = request.YearsOfExperience
        };

        var result = await _updateDoctorHandler.HandleAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return NotFound(ApiResponse.ErrorResponse(result.Error, "Doctor not found"));
            return BadRequest(ApiResponse.ErrorResponse(result.Error, "Failed to update doctor"));
        }

        return Ok(ApiResponse.SuccessResponse("Doctor profile updated successfully"));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.AdminOrDoctor)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteDoctor(int id, CancellationToken cancellationToken)
    {
        if (User.IsInRole(AppRoles.Doctor) && User.GetDoctorId() != id)
            return Forbid();

        _logger.LogInformation("Deactivating doctor {DoctorId}", id);

        var command = new DeactivateDoctorCommand { DoctorId = id };
        var result = await _deactivateDoctorHandler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to deactivate doctor {DoctorId}: {Error}", id, result.Error);
            return BadRequest(ApiResponse.ErrorResponse(result.Error, "Doctor already deactivated"));
        }

        // Self-service: clear User.DoctorId claim source.
        if (User.IsInRole(AppRoles.Doctor))
        {
            var userId = User.GetUserId();
            var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
            if (user is not null && user.DoctorId == id)
            {
                user.UnlinkDoctor();
                await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        _logger.LogInformation("Doctor {DoctorId} deactivated successfully", id);
        return NoContent();
    }

    /// <summary>
    /// Cache-aside paginated list. Keys versioned by generation so invalidation is O(1).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DoctorDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllDoctors(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        NormalizePaging(ref pageNumber, ref pageSize);

        var pagedDtos = await _doctorCache.GetDoctorPageAsync(
            CacheKeys.DoctorListFilterAll,
            pageNumber,
            pageSize,
            async ct =>
            {
                var paged = await _unitOfWork.Doctors.GetPagedAsync(pageNumber, pageSize, ct);
                var items = paged.Items.Select(MapToDto).ToList();
                return new PagedResult<DoctorDto>(items, paged.PageNumber, paged.PageSize, paged.TotalCount);
            },
            cancellationToken);

        return Ok(ApiResponse<PagedResult<DoctorDto>>.SuccessResponse(
            pagedDtos,
            $"Retrieved page {pagedDtos.PageNumber} of {pagedDtos.TotalPages} ({pagedDtos.Items.Count()} items)"));
    }

    [HttpGet("active")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DoctorDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveDoctors(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        NormalizePaging(ref pageNumber, ref pageSize);

        var pagedDtos = await _doctorCache.GetDoctorPageAsync(
            CacheKeys.DoctorListFilterActive,
            pageNumber,
            pageSize,
            async ct =>
            {
                var paged = await _unitOfWork.Doctors.GetPagedActiveAsync(pageNumber, pageSize, ct);
                var items = paged.Items.Select(MapToDto).ToList();
                return new PagedResult<DoctorDto>(items, paged.PageNumber, paged.PageSize, paged.TotalCount);
            },
            cancellationToken);

        return Ok(ApiResponse<PagedResult<DoctorDto>>.SuccessResponse(
            pagedDtos,
            $"Retrieved page {pagedDtos.PageNumber} of {pagedDtos.TotalPages} ({pagedDtos.Items.Count()} active doctor(s))"));
    }

    [HttpGet("accepting-patients")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DoctorDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDoctorsAcceptingPatients(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        NormalizePaging(ref pageNumber, ref pageSize);

        var pagedDtos = await _doctorCache.GetDoctorPageAsync(
            CacheKeys.DoctorListFilterAccepting,
            pageNumber,
            pageSize,
            async ct =>
            {
                var paged = await _unitOfWork.Doctors.GetPagedAcceptingPatientsAsync(pageNumber, pageSize, ct);
                var items = paged.Items.Select(MapToDto).ToList();
                return new PagedResult<DoctorDto>(items, paged.PageNumber, paged.PageSize, paged.TotalCount);
            },
            cancellationToken);

        return Ok(ApiResponse<PagedResult<DoctorDto>>.SuccessResponse(
            pagedDtos,
            $"Retrieved page {pagedDtos.PageNumber} of {pagedDtos.TotalPages} ({pagedDtos.Items.Count()} doctor(s) accepting patients)"));
    }

    private static void NormalizePaging(ref int pageNumber, ref int pageSize)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;
    }

    private static DoctorDto MapToDto(Doctor doctor) => new()
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

    private static DoctorScheduleDto MapToScheduleDto(Doctor doctor) => new()
    {
        DoctorId = doctor.Id,
        IsActive = doctor.IsActive,
        IsAcceptingPatients = doctor.IsAcceptingPatients,
        WeeklySchedule = doctor.WeeklySchedule
            .OrderBy(h => h.DayOfWeek)
            .Select(h => new WorkingHoursDto
            {
                DayOfWeek = h.DayOfWeek,
                IsWorkingDay = h.IsWorkingDay,
                StartTime = h.StartTime?.ToString("HH:mm"),
                EndTime = h.EndTime?.ToString("HH:mm")
            })
            .ToList()
    };
}
