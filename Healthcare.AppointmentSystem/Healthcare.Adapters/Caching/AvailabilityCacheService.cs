using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Caching;
using Microsoft.Extensions.Logging;

namespace Healthcare.Adapters.Caching;

/// <summary>
/// Day-level booked-slot cache. Short TTL + explicit invalidation on appointment lifecycle events.
/// </summary>
public sealed class AvailabilityCacheService : IAvailabilityCacheService
{
    private readonly ICacheService _cache;
    private readonly CacheSettings _settings;
    private readonly ILogger<AvailabilityCacheService> _logger;

    public AvailabilityCacheService(
        ICacheService cache,
        CacheSettings settings,
        ILogger<AvailabilityCacheService> logger)
    {
        _cache = cache;
        _settings = settings;
        _logger = logger;
    }

    public Task<DoctorDayAvailabilityDto?> GetDayAsync(
        int doctorId,
        DateOnly date,
        Func<CancellationToken, Task<DoctorDayAvailabilityDto?>> factory,
        CancellationToken cancellationToken = default) =>
        _cache.GetOrCreateAsync(
            CacheKeys.DoctorDayAvailability(doctorId, date),
            factory,
            _settings.AvailabilityTtl,
            cancellationToken);

    public async Task InvalidateDayAsync(int doctorId, DateOnly date, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(CacheKeys.DoctorDayAvailability(doctorId, date), cancellationToken);
        _logger.LogDebug("Invalidated availability cache for doctor {DoctorId} on {Date}", doctorId, date);
    }

    public async Task InvalidateDoctorAsync(int doctorId, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveByPrefixAsync(CacheKeys.DoctorAvailabilityPrefix(doctorId), cancellationToken);
        _logger.LogInformation("Invalidated all availability cache for doctor {DoctorId}", doctorId);
    }
}
