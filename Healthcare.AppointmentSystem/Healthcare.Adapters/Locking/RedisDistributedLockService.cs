using Healthcare.Application.Ports.Locking;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Healthcare.Adapters.Locking;

/// <summary>
/// Redis implementation of distributed locking.
/// </summary>
/// <remarks>
/// Design Pattern: Adapter Pattern
/// 
/// Uses Redis SET with NX (Not eXists) and PX (exPire milliseconds) options:
/// SET lock_key unique_value NX PX 10000
/// 
/// Thread Safety: Redis operations are atomic
/// Fault Tolerance: Locks auto-expire to prevent deadlocks
/// </remarks>
public sealed class RedisDistributedLockService : IDistributedLockService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisDistributedLockService> _logger;

    public RedisDistributedLockService(
        IConnectionMultiplexer redis,
        ILogger<RedisDistributedLockService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<ILockHandle?> AcquireLockAsync(
        string lockKey,
        TimeSpan expirationTime,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var lockValue = Guid.NewGuid().ToString(); // Unique identifier for this lock acquisition

        _logger.LogDebug("Attempting to acquire lock: {LockKey}", lockKey);

        // Try to acquire lock with Redis SET NX (set if not exists)
        var acquired = await db.StringSetAsync(
            lockKey,
            lockValue,
            expirationTime,
            When.NotExists);

        if (!acquired)
        {
            _logger.LogWarning("Failed to acquire lock: {LockKey} (already held)", lockKey);
            return null;
        }

        _logger.LogInformation("Lock acquired: {LockKey} with value {LockValue}", lockKey, lockValue);

        return new RedisLockHandle(db, lockKey, lockValue, _logger);
    }
}

/// <summary>
/// Redis lock handle that auto-releases on disposal.
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
        {
            return;
        }

        try
        {
            // Lua script to ensure we only delete OUR lock (not someone else's)
            var script = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";

            var result = await _db.ScriptEvaluateAsync(
                script,
                new RedisKey[] { LockKey },
                new RedisValue[] { _lockValue });

            if ((int)result == 1)
            {
                _logger.LogInformation("Lock released: {LockKey}", LockKey);
            }
            else
            {
                _logger.LogWarning(
                    "Lock {LockKey} was already released or expired",
                    LockKey);
            }

            _released = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing lock: {LockKey}", LockKey);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await ReleaseAsync();
    }
}