namespace Healthcare.Adapters.Caching;

/// <summary>TTL and stampede settings for Redis cache-aside.</summary>
public sealed class CacheSettings
{
    public const string SectionName = "Cache";

    /// <summary>Master switch — when false, GetOrCreate always hits the factory.</summary>
    public bool Enabled { get; set; } = true;

    public int DefaultTtlSeconds { get; set; } = 300;

    /// <summary>Doctor by-id and list pages.</summary>
    public int DoctorCatalogTtlSeconds { get; set; } = 300;

    /// <summary>Weekly schedule (working hours).</summary>
    public int DoctorScheduleTtlSeconds { get; set; } = 1800;

    /// <summary>Day-level booked slots (must be short — bookings change often).</summary>
    public int AvailabilityTtlSeconds { get; set; } = 60;

    /// <summary>How long a stampede lock is held while loading from source.</summary>
    public int StampedeLockSeconds { get; set; } = 10;

    /// <summary>Poll attempts while waiting for another loader to fill the cache.</summary>
    public int StampedeWaitAttempts { get; set; } = 5;

    /// <summary>Base delay (ms) between stampede wait polls (multiplied by attempt).</summary>
    public int StampedeWaitBaseDelayMs { get; set; } = 40;

    public TimeSpan DefaultTtl => TimeSpan.FromSeconds(Math.Max(5, DefaultTtlSeconds));
    public TimeSpan DoctorCatalogTtl => TimeSpan.FromSeconds(Math.Max(10, DoctorCatalogTtlSeconds));
    public TimeSpan DoctorScheduleTtl => TimeSpan.FromSeconds(Math.Max(30, DoctorScheduleTtlSeconds));
    public TimeSpan AvailabilityTtl => TimeSpan.FromSeconds(Math.Max(15, AvailabilityTtlSeconds));
    public TimeSpan StampedeLock => TimeSpan.FromSeconds(Math.Max(2, StampedeLockSeconds));
}
