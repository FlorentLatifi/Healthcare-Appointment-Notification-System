using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Caching;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Healthcare.Adapters.Caching;

public sealed class InMemoryDoctorCacheService : IDoctorCacheService, IDisposable
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ILogger<InMemoryDoctorCacheService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly TimeSpan _defaultTtl;

    private sealed class CacheEntry
    {
        public string Json { get; }
        public DateTime ExpiresAt { get; }

        public CacheEntry(string json, TimeSpan ttl)
        {
            Json = json;
            ExpiresAt = DateTime.UtcNow.Add(ttl);
        }

        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    }

    public InMemoryDoctorCacheService(ILogger<InMemoryDoctorCacheService> logger, TimeSpan? defaultTtl = null)
    {
        _logger = logger;
        _defaultTtl = defaultTtl ?? TimeSpan.FromMinutes(5);
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public Task<IReadOnlyList<DoctorDto>?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired)
        {
            _logger.LogDebug("Cache hit for key {CacheKey}", key);
            var result = JsonSerializer.Deserialize<List<DoctorDto>>(entry.Json, _jsonOptions);
            return Task.FromResult<IReadOnlyList<DoctorDto>?>(result);
        }

        if (entry != null)
        {
            _cache.TryRemove(key, out _);
        }

        _logger.LogDebug("Cache miss for key {CacheKey}", key);
        return Task.FromResult<IReadOnlyList<DoctorDto>?>(null);
    }

    public Task SetAsync(string key, IReadOnlyList<DoctorDto> doctors, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(doctors, _jsonOptions);
        _cache[key] = new CacheEntry(json, _defaultTtl);
        _logger.LogDebug("Cached {Count} doctors under key {CacheKey}", doctors.Count, key);
        return Task.CompletedTask;
    }

    public Task InvalidateAllAsync(CancellationToken cancellationToken = default)
    {
        _cache.Clear();
        _logger.LogInformation("Invalidated all in-memory doctor cache");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _cache.Clear();
    }
}
