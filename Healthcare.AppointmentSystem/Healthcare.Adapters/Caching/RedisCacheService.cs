using System.Text.Json;
using Healthcare.Application.Ports.Caching;
using Healthcare.Application.Ports.Locking;
using Healthcare.Adapters.Locking;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Healthcare.Adapters.Caching;

/// <summary>
/// Redis cache-aside with instance-prefixed keys, generation counters, and lock-based stampede protection.
/// </summary>
public sealed class RedisCacheService : ICacheService
{
    private readonly IDatabase _db;
    private readonly IServer? _server;
    private readonly IDistributedLockService _lockService;
    private readonly CacheSettings _settings;
    private readonly RedisSettings _redisSettings;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly JsonSerializerOptions _json;

    public RedisCacheService(
        IConnectionMultiplexer redis,
        IDistributedLockService lockService,
        CacheSettings settings,
        RedisSettings redisSettings,
        ILogger<RedisCacheService> logger)
    {
        _db = redis.GetDatabase();
        _lockService = lockService;
        _settings = settings;
        _redisSettings = redisSettings;
        _logger = logger;
        _json = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        try
        {
            var endpoint = redis.GetEndPoints().FirstOrDefault();
            _server = endpoint is null ? null : redis.GetServer(endpoint);
        }
        catch
        {
            _server = null;
        }
    }

    public async Task<CacheLookup<T>> TryGetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
            return CacheLookup<T>.Miss();

        try
        {
            var value = await _db.StringGetAsync(Prefixed(key));
            if (!value.HasValue)
                return CacheLookup<T>.Miss();

            var deserialized = JsonSerializer.Deserialize<T>((string)value!, _json);
            return CacheLookup<T>.Hit(deserialized);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache GET failed for {Key}", key);
            return CacheLookup<T>.Miss();
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
            return;

        try
        {
            var json = JsonSerializer.Serialize(value, _json);
            await _db.StringSetAsync(Prefixed(key), json, ttl ?? _settings.DefaultTtl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache SET failed for {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _db.KeyDeleteAsync(Prefixed(key));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache DEL failed for {Key}", key);
        }
    }

    public async Task RemoveByPrefixAsync(string keyPrefix, CancellationToken cancellationToken = default)
    {
        if (_server is null)
        {
            _logger.LogDebug("Redis server API unavailable; prefix delete skipped for {Prefix}", keyPrefix);
            return;
        }

        try
        {
            var pattern = Prefixed(keyPrefix) + "*";
            var keys = new List<RedisKey>();
            await foreach (var key in _server.KeysAsync(pattern: pattern))
            {
                keys.Add(key);
                if (keys.Count >= 200)
                {
                    await _db.KeyDeleteAsync(keys.ToArray());
                    keys.Clear();
                }
            }

            if (keys.Count > 0)
                await _db.KeyDeleteAsync(keys.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache prefix delete failed for {Prefix}", keyPrefix);
        }
    }

    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
            return await factory(cancellationToken);

        var existing = await TryGetAsync<T>(key, cancellationToken);
        if (existing.Found)
            return existing.Value;

        var lockKey = CacheKeys.StampedeLock(key);
        await using var handle = await _lockService.AcquireLockAsync(
            lockKey,
            _settings.StampedeLock,
            cancellationToken);

        if (handle is null)
        {
            // Another instance is loading — wait for fill, then fall back to factory.
            for (var attempt = 1; attempt <= _settings.StampedeWaitAttempts; attempt++)
            {
                await Task.Delay(_settings.StampedeWaitBaseDelayMs * attempt, cancellationToken);
                var again = await TryGetAsync<T>(key, cancellationToken);
                if (again.Found)
                    return again.Value;
            }

            _logger.LogDebug("Stampede wait exhausted for {Key}; loading without lock", key);
            return await factory(cancellationToken);
        }

        // Double-check after acquiring single-flight lock.
        existing = await TryGetAsync<T>(key, cancellationToken);
        if (existing.Found)
            return existing.Value;

        var value = await factory(cancellationToken);
        if (value is not null)
            await SetAsync(key, value, ttl, cancellationToken);

        return value;
    }

    public async Task<long> IncrementGenerationAsync(string generationKey, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.StringIncrementAsync(Prefixed(generationKey));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Generation INCR failed for {Key}", generationKey);
            return DateTime.UtcNow.Ticks;
        }
    }

    public async Task<long> GetGenerationAsync(string generationKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await _db.StringGetAsync(Prefixed(generationKey));
            if (!value.HasValue)
                return 0;
            return (long)value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Generation GET failed for {Key}", generationKey);
            return 0;
        }
    }

    private string Prefixed(string key) =>
        string.IsNullOrEmpty(_redisSettings.InstanceName)
            ? key
            : _redisSettings.InstanceName + key;
}
