namespace Healthcare.Adapters.Locking;

/// <summary>
/// Configuration settings for Redis.
/// </summary>
public sealed class RedisSettings
{
    /// <summary>
    /// Redis connection string.
    /// </summary>
    /// <example>localhost:6379</example>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Instance name prefix for all Redis keys.
    /// </summary>
    /// <remarks>
    /// Useful for multi-environment isolation (Dev, Staging, Prod).
    /// All keys will be prefixed with this value.
    /// </remarks>
    /// <example>HealthcareApp:Prod:</example>
    public string InstanceName { get; set; } = "HealthcareApp:";

    /// <summary>
    /// Default lock expiration time in seconds.
    /// </summary>
    /// <remarks>
    /// Prevents deadlocks by auto-releasing locks after timeout.
    /// Should be longer than typical operation time.
    /// </remarks>
    public int DefaultLockExpirationSeconds { get; set; } = 30;
}
