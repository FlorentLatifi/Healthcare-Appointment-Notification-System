// Healthcare.Adapters/Locking/InMemoryLockService.cs

using Healthcare.Application.Ports.Locking;

namespace Healthcare.Adapters.Locking;

/// <summary>
/// In-memory implementation of distributed locking (for development/testing).
/// </summary>
/// <remarks>
/// ⚠️ NOT thread-safe across multiple instances.
/// Use ONLY for local development/testing.
/// For production, use RedisDistributedLockService.
/// </remarks>
public sealed class InMemoryLockService : IDistributedLockService
{
    private readonly Dictionary<string, DateTime> _locks = new();
    private readonly object _lockObj = new();

    public Task<ILockHandle?> AcquireLockAsync(
        string lockKey,
        TimeSpan expirationTime,
        CancellationToken cancellationToken = default)
    {
        lock (_lockObj)
        {
            // Clean expired locks
            var now = DateTime.UtcNow;
            var expiredKeys = _locks
                .Where(kvp => kvp.Value < now)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _locks.Remove(key);
            }

            // Try to acquire lock
            if (_locks.ContainsKey(lockKey))
            {
                return Task.FromResult<ILockHandle?>(null); // Lock already held
            }

            // Acquire lock
            _locks[lockKey] = now.Add(expirationTime);
            return Task.FromResult<ILockHandle?>(new InMemoryLockHandle(this, lockKey, now));
        }
    }

    internal void ReleaseLock(string lockKey)
    {
        lock (_lockObj)
        {
            _locks.Remove(lockKey);
        }
    }
}

internal sealed class InMemoryLockHandle : ILockHandle
{
    private readonly InMemoryLockService _service;
    private bool _released;

    public string LockKey { get; }
    public DateTime AcquiredAt { get; }

    public InMemoryLockHandle(InMemoryLockService service, string lockKey, DateTime acquiredAt)
    {
        _service = service;
        LockKey = lockKey;
        AcquiredAt = acquiredAt;
    }

    public Task ReleaseAsync()
    {
        if (!_released)
        {
            _service.ReleaseLock(LockKey);
            _released = true;
        }
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await ReleaseAsync();
    }
}