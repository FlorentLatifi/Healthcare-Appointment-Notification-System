namespace Healthcare.Application.Ports.Caching;

/// <summary>
/// Generic cache-aside store with stampede-safe <see cref="GetOrCreateAsync{T}"/>.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Returns (false, default) on miss; (true, value) on hit (value may be default for cached nulls if supported).
    /// </summary>
    Task<CacheLookup<T>> TryGetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all keys matching a prefix (best-effort; Redis uses SCAN).
    /// </summary>
    Task RemoveByPrefixAsync(string keyPrefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache-aside with single-flight: only one caller loads on miss (stampede protection).
    /// </summary>
    Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically increments a generation counter used to version list/catalog keys.
    /// </summary>
    Task<long> IncrementGenerationAsync(string generationKey, CancellationToken cancellationToken = default);

    Task<long> GetGenerationAsync(string generationKey, CancellationToken cancellationToken = default);
}

/// <summary>Result of a cache lookup.</summary>
public readonly record struct CacheLookup<T>(bool Found, T? Value)
{
    public static CacheLookup<T> Miss() => new(false, default);
    public static CacheLookup<T> Hit(T? value) => new(true, value);
}
