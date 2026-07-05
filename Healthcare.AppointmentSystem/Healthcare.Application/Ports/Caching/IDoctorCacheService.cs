using Healthcare.Application.DTOs;

namespace Healthcare.Application.Ports.Caching;

public interface IDoctorCacheService
{
    Task<IReadOnlyList<DoctorDto>?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task SetAsync(string key, IReadOnlyList<DoctorDto> doctors, CancellationToken cancellationToken = default);

    Task InvalidateAllAsync(CancellationToken cancellationToken = default);
}
