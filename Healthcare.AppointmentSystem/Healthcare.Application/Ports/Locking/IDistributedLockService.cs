namespace Healthcare.Application.Ports.Locking;

/// <summary>
/// Distributed lock service for preventing race conditions.
/// </summary>
/// <remarks>
/// Design Pattern: Adapter Pattern
/// 
/// This PORT abstracts distributed locking mechanism.
/// The ADAPTER can be implemented with:
/// - Redis (StackExchange.Redis)
/// - SQL Server (sp_getapplock)
/// - Azure Cosmos DB (leases)
/// 
/// Use Case: Prevent double-booking appointments
/// 
/// Example:
/// <code>
/// var lockKey = $"appointment:doctor:{doctorId}:time:{scheduledTime:yyyyMMddHHmm}";
/// await using var lockHandle = await _lockService.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(10));
/// 
/// if (lockHandle == null)
/// {
///     return Result.Failure("Another booking is in progress. Please try again.");
/// }
/// 
/// // Perform booking logic here - guaranteed to be exclusive
/// </code>
/// </remarks>
public interface IDistributedLockService
{
    /// <summary>
    /// Attempts to acquire a distributed lock.
    /// </summary>
    /// <param name="lockKey">Unique identifier for the lock.</param>
    /// <param name="expirationTime">How long to hold the lock before auto-release.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Lock handle if successful, null if lock couldn't be acquired.</returns>
    Task<ILockHandle?> AcquireLockAsync(
        string lockKey,
        TimeSpan expirationTime,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a held distributed lock.
/// </summary>
/// <remarks>
/// IAsyncDisposable pattern ensures lock is released when out of scope.
/// Use with 'await using' statement for automatic cleanup.
/// </remarks>
public interface ILockHandle : IAsyncDisposable
{
    /// <summary>
    /// The lock key that was acquired.
    /// </summary>
    string LockKey { get; }

    /// <summary>
    /// When the lock was acquired.
    /// </summary>
    DateTime AcquiredAt { get; }

    /// <summary>
    /// Manually releases the lock before expiration.
    /// </summary>
    Task ReleaseAsync();
}