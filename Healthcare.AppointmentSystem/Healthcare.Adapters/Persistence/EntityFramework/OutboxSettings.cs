namespace Healthcare.Adapters.Persistence.EntityFramework;

/// <summary>
/// Transactional outbox configuration.
/// Prefer ON in Production for at-least-once delivery; OFF in Development for immediate handlers.
/// </summary>
public sealed class OutboxSettings
{
    public const string SectionName = "Outbox";

    public bool UseOutboxForDomainEvents { get; set; }

    /// <summary>Base poll interval when the circuit is closed and work is idle.</summary>
    public int RelayIntervalSeconds { get; set; } = 10;

    /// <summary>Maximum delivery attempts before dead-lettering (including the first try).</summary>
    public int MaxRetryAttempts { get; set; } = 5;

    public int BatchSize { get; set; } = 50;

    /// <summary>Base delay for exponential backoff (attempt 1 uses up to this value with jitter).</summary>
    public int BaseRetryDelaySeconds { get; set; } = 5;

    /// <summary>Cap for exponential backoff.</summary>
    public int MaxRetryDelaySeconds { get; set; } = 300;

    /// <summary>How long a Processing claim may stay open before being reclaimed.</summary>
    public int ProcessingLeaseSeconds { get; set; } = 120;

    /// <summary>Consecutive batch-level failures before opening the circuit.</summary>
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>How long the circuit stays open (relay pauses claiming work).</summary>
    public int CircuitBreakerBreakSeconds { get; set; } = 60;

    public TimeSpan BaseRetryDelay => TimeSpan.FromSeconds(Math.Max(1, BaseRetryDelaySeconds));
    public TimeSpan MaxRetryDelay => TimeSpan.FromSeconds(Math.Max(BaseRetryDelaySeconds, MaxRetryDelaySeconds));
    public TimeSpan ProcessingLease => TimeSpan.FromSeconds(Math.Max(30, ProcessingLeaseSeconds));
    public TimeSpan CircuitBreakDuration => TimeSpan.FromSeconds(Math.Max(10, CircuitBreakerBreakSeconds));
}
