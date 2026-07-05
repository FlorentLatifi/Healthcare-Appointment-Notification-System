using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Caching;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace Healthcare.Adapters.Caching;

public sealed class RedisDoctorCacheService : IDoctorCacheService
{
    private readonly IDatabase? _redis;
    private readonly ILogger<RedisDoctorCacheService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string CacheKeyPrefix = "doctor_cache:";
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    private static readonly string[] AllCacheKeys =
    {
        $"{CacheKeyPrefix}all",
        $"{CacheKeyPrefix}active",
        $"{CacheKeyPrefix}accepting-patients"
    };

    public RedisDoctorCacheService(IConnectionMultiplexer? redis, ILogger<RedisDoctorCacheService> logger)
    {
        _redis = redis?.GetDatabase();
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<IReadOnlyList<DoctorDto>?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_redis == null)
        {
            return null;
        }

        try
        {
            var redisKey = $"{CacheKeyPrefix}{key}";
            var value = await _redis.StringGetAsync(redisKey);

            if (!value.HasValue)
            {
                return null;
            }

            _logger.LogDebug("Cache hit for key {CacheKey}", redisKey);
            return JsonSerializer.Deserialize<List<DoctorDto>>(value!, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read from Redis cache for key {CacheKey}", key);
            return null;
        }
    }

    public async Task SetAsync(string key, IReadOnlyList<DoctorDto> doctors, CancellationToken cancellationToken = default)
    {
        if (_redis == null)
        {
            return;
        }

        try
        {
            var redisKey = $"{CacheKeyPrefix}{key}";
            var json = JsonSerializer.Serialize(doctors, _jsonOptions);
            await _redis.StringSetAsync(redisKey, json, DefaultTtl);

            _logger.LogDebug("Cached {Count} doctors under key {CacheKey} for {Ttl}", doctors.Count, redisKey, DefaultTtl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write to Redis cache for key {CacheKey}", key);
        }
    }

    public async Task InvalidateAllAsync(CancellationToken cancellationToken = default)
    {
        if (_redis == null)
        {
            return;
        }

        try
        {
            var batch = _redis.CreateBatch();
            var tasks = AllCacheKeys.Select(key => batch.KeyDeleteAsync(key));
            batch.Execute();
            await Task.WhenAll(tasks);

            _logger.LogInformation("Invalidated all doctor cache keys");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate doctor cache");
        }
    }
}
