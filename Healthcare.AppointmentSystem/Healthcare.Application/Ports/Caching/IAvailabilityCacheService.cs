using Healthcare.Application.DTOs;

namespace Healthcare.Application.Ports.Caching;

/// <summary>
/// Cache for doctor day-level booked slots (availability / schedule views).
/// Not a source of truth for booking — booking always re-checks under a distributed lock + DB.
/// </summary>
public interface IAvailabilityCacheService
{
    Task<DoctorDayAvailabilityDto?> GetDayAsync(
        int doctorId,
        DateOnly date,
        Func<CancellationToken, Task<DoctorDayAvailabilityDto?>> factory,
        CancellationToken cancellationToken = default);

    /// <summary>Drop one day (e.g. after book/cancel on that date).</summary>
    Task InvalidateDayAsync(int doctorId, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>Drop all cached days for a doctor (schedule change or bulk updates).</summary>
    Task InvalidateDoctorAsync(int doctorId, CancellationToken cancellationToken = default);
}
