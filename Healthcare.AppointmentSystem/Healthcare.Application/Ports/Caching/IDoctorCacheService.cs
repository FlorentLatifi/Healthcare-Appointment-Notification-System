using Healthcare.Application.Common;
using Healthcare.Application.DTOs;

namespace Healthcare.Application.Ports.Caching;

/// <summary>
/// Domain-oriented cache for doctor catalog + schedule (cache-aside, stampede-safe).
/// </summary>
public interface IDoctorCacheService
{
    Task<DoctorDto?> GetDoctorByIdAsync(
        int doctorId,
        Func<CancellationToken, Task<DoctorDto?>> factory,
        CancellationToken cancellationToken = default);

    Task<PagedResult<DoctorDto>> GetDoctorPageAsync(
        string filter,
        int pageNumber,
        int pageSize,
        Func<CancellationToken, Task<PagedResult<DoctorDto>>> factory,
        CancellationToken cancellationToken = default);

    Task<DoctorScheduleDto?> GetScheduleAsync(
        int doctorId,
        Func<CancellationToken, Task<DoctorScheduleDto?>> factory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates catalog lists (generation bump), optional doctor id, and schedule.
    /// </summary>
    Task InvalidateDoctorAsync(int? doctorId = null, CancellationToken cancellationToken = default);

    /// <summary>Invalidates all doctor list pages via generation bump + optional full catalog purge.</summary>
    Task InvalidateAllAsync(CancellationToken cancellationToken = default);
}
