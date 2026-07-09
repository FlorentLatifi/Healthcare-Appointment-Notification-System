namespace Healthcare.Adapters.Persistence.EntityFramework;

/// <summary>
/// Trade-off: writing domain events to an outbox table (rather than dispatching
/// in-process) guarantees at-least-once delivery and survives process restarts,
/// at the cost of ~100 ms of latency per SaveChangesAsync and a background
/// relay poll every N seconds.  Keep ON in Production, OFF in Development so
/// breakpoints inside handlers work without having to wait for the relay cycle.
/// </summary>
public class OutboxSettings
{
    public bool UseOutboxForDomainEvents { get; set; }
    public int RelayIntervalSeconds { get; set; } = 10;
    public int MaxRetryAttempts { get; set; } = 5;
    public int BatchSize { get; set; } = 50;
}
