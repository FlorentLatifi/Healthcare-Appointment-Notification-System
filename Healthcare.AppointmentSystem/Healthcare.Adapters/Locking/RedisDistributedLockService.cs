using Healthcare.Application.Ports.Locking;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Healthcare.Adapters.Locking;

/// <summary>
/// Redis distributed lock: SET key value NX PX expiry + Lua compare-and-del release.
/// Keys are prefixed with <see cref="RedisSettings.InstanceName"/>.
/// </summary>
public sealed class RedisDistributedLockService : IDistributedLockService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisSettings _settings;
    private readonly ILogger<RedisDistributedLockService> _logger;

    internal const string ReleaseScript = """
        if redis.call('get', KEYS[1]) == ARGV[1] then
            return redis.call('del', KEYS[1])
        else
            return 0
        end
        """;

    public RedisDistributedLockService(
        IConnectionMultiplexer redis,
        RedisSettings settings,
        ILogger<RedisDistributedLockService> logger)
    {
        _redis = redis;
        _settings = settings;
        _logger = logger;
    }

    public async Task<ILockHandle?> AcquireLockAsync(
        string lockKey,
        TimeSpan expirationTime,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(lockKey))
            throw new ArgumentException("Lock key is required.", nameof(lockKey));

        if (expirationTime <= TimeSpan.Zero)
            expirationTime = TimeSpan.FromSeconds(Math.Max(1, _settings.DefaultLockExpirationSeconds));

        var db = _redis.GetDatabase();
        var fullKey = Prefixed(lockKey);
        var lockValue = Guid.NewGuid().ToString("N");

        _logger.LogDebug("Attempting lock acquire: {LockKey}", fullKey);

        var acquired = await db.StringSetAsync(
            fullKey,
            lockValue,
            expirationTime,
            When.NotExists);

        if (!acquired)
        {
            _logger.LogDebug("Lock not acquired (held): {LockKey}", fullKey);
            return null;
        }

        _logger.LogDebug("Lock acquired: {LockKey}", fullKey);
        return new RedisLockHandle(db, fullKey, lockValue, _logger);
    }

    private string Prefixed(string lockKey)
    {
        if (!string.IsNullOrEmpty(_settings.InstanceName) &&
            !lockKey.StartsWith(_settings.InstanceName, StringComparison.Ordinal))
        {
            return _settings.InstanceName + lockKey;
        }

        return lockKey;
    }
}

/// <summary>
/// Redis lock handle that only deletes the key if the token still matches.
/// </summary>
internal sealed class RedisLockHandle : ILockHandle
{
    private readonly IDatabase _db;
    private readonly string _lockValue;
    private readonly ILogger _logger;
    private bool _released;

    public string LockKey { get; }
    public DateTime AcquiredAt { get; }

    public RedisLockHandle(
        IDatabase db,
        string lockKey,
        string lockValue,
        ILogger logger)
    {
        _db = db;
        LockKey = lockKey;
        _lockValue = lockValue;
        _logger = logger;
        AcquiredAt = DateTime.UtcNow;
    }

    public async Task ReleaseAsync()
    {
        if (_released)
            return;

        try
        {
            var result = await _db.ScriptEvaluateAsync(
                RedisDistributedLockService.ReleaseScript,
                new RedisKey[] { LockKey },
                new RedisValue[] { _lockValue });

            if ((int)result == 1)
                _logger.LogDebug("Lock released: {LockKey}", LockKey);
            else
                _logger.LogWarning("Lock {LockKey} already expired or stolen", LockKey);

            _released = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing lock: {LockKey}", LockKey);
            throw;
        }
    }

    public async ValueTask DisposeAsync() => await ReleaseAsync();
}
