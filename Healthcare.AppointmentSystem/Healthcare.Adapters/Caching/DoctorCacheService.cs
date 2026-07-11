using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Caching;
using Microsoft.Extensions.Logging;

namespace Healthcare.Adapters.Caching;

/// <summary>
/// Doctor catalog + schedule cache-aside on top of <see cref="ICacheService"/>.
/// List invalidation uses a generation counter (O(1)) so old pages expire naturally.
/// </summary>
public sealed class DoctorCacheService : IDoctorCacheService
{
    private readonly ICacheService _cache;
    private readonly CacheSettings _settings;
    private readonly ILogger<DoctorCacheService> _logger;

    public DoctorCacheService(
        ICacheService cache,
        CacheSettings settings,
        ILogger<DoctorCacheService> logger)
    {
        _cache = cache;
        _settings = settings;
        _logger = logger;
    }

    public Task<DoctorDto?> GetDoctorByIdAsync(
        int doctorId,
        Func<CancellationToken, Task<DoctorDto?>> factory,
        CancellationToken cancellationToken = default) =>
        _cache.GetOrCreateAsync(
            CacheKeys.DoctorById(doctorId),
            factory,
            _settings.DoctorCatalogTtl,
            cancellationToken);

    public async Task<PagedResult<DoctorDto>> GetDoctorPageAsync(
        string filter,
        int pageNumber,
        int pageSize,
        Func<CancellationToken, Task<PagedResult<DoctorDto>>> factory,
        CancellationToken cancellationToken = default)
    {
        var gen = await _cache.GetGenerationAsync(CacheKeys.DoctorListGeneration, cancellationToken);
        var key = CacheKeys.DoctorList(filter, gen, pageNumber, pageSize);

        // Envelope type is JSON-friendly (PagedResult has get-only ctor props).
        var cached = await _cache.GetOrCreateAsync(
            key,
            async ct =>
            {
                var page = await factory(ct);
                return new CachedDoctorPage
                {
                    Items = page.Items.ToList(),
                    PageNumber = page.PageNumber,
                    PageSize = page.PageSize,
                    TotalCount = page.TotalCount
                };
            },
            _settings.DoctorCatalogTtl,
            cancellationToken);

        if (cached is null)
            return new PagedResult<DoctorDto>(Array.Empty<DoctorDto>(), pageNumber, pageSize, 0);

        return new PagedResult<DoctorDto>(cached.Items, cached.PageNumber, cached.PageSize, cached.TotalCount);
    }

    private sealed class CachedDoctorPage
    {
        public List<DoctorDto> Items { get; set; } = new();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }

    public Task<DoctorScheduleDto?> GetScheduleAsync(
        int doctorId,
        Func<CancellationToken, Task<DoctorScheduleDto?>> factory,
        CancellationToken cancellationToken = default) =>
        _cache.GetOrCreateAsync(
            CacheKeys.DoctorSchedule(doctorId),
            factory,
            _settings.DoctorScheduleTtl,
            cancellationToken);

    public async Task InvalidateDoctorAsync(int? doctorId = null, CancellationToken cancellationToken = default)
    {
        // Bump generation → all list keys become unreachable.
        var gen = await _cache.IncrementGenerationAsync(CacheKeys.DoctorListGeneration, cancellationToken);
        _logger.LogInformation("Doctor list generation bumped to {Generation}", gen);

        if (doctorId is int id)
        {
            await _cache.RemoveAsync(CacheKeys.DoctorById(id), cancellationToken);
            await _cache.RemoveAsync(CacheKeys.DoctorSchedule(id), cancellationToken);
            _logger.LogInformation("Invalidated doctor {DoctorId} by-id and schedule cache", id);
        }
    }

    public async Task InvalidateAllAsync(CancellationToken cancellationToken = default)
    {
        await _cache.IncrementGenerationAsync(CacheKeys.DoctorListGeneration, cancellationToken);
        await _cache.RemoveByPrefixAsync(CacheKeys.DoctorCatalogPrefix, cancellationToken);
        await _cache.RemoveByPrefixAsync(CacheKeys.DoctorSchedulePrefix, cancellationToken);
        _logger.LogInformation("Invalidated all doctor catalog and schedule cache entries");
    }
}
