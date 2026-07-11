namespace Healthcare.Adapters.Events;

/// <summary>
/// Lifecycle of a transactional outbox message.
/// </summary>
public enum OutboxMessageStatus
{
    /// <summary>Ready (or scheduled) for delivery when <c>NextAttemptAt</c> is due.</summary>
    Pending = 0,

    /// <summary>Claimed by a relay worker; lease-protected.</summary>
    Processing = 1,

    /// <summary>Successfully dispatched to all handlers (terminal).</summary>
    Processed = 2,

    /// <summary>Exhausted retries or non-retryable failure (terminal until manual requeue).</summary>
    DeadLetter = 3
}
