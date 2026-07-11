namespace Healthcare.Adapters.Events;

/// <summary>
/// Persistent domain-event envelope for the transactional outbox pattern.
/// </summary>
public sealed class OutboxMessage
{
    public int Id { get; private set; }

    /// <summary>
    /// Domain event's <c>EventId</c> — unique for idempotent insert and processing.
    /// </summary>
    public Guid MessageId { get; private set; }

    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime OccurredOn { get; private set; }

    public OutboxMessageStatus Status { get; private set; }

    public DateTime? ProcessedAt { get; private set; }
    public DateTime? DeadLetteredAt { get; private set; }

    /// <summary>Earliest UTC time this message may be attempted again.</summary>
    public DateTime NextAttemptAt { get; private set; }

    public string? Error { get; private set; }
    public int RetryCount { get; private set; }

    /// <summary>When a worker claimed the row for processing (lease start).</summary>
    public DateTime? ProcessingStartedAt { get; private set; }

    private OutboxMessage() { }

    public OutboxMessage(string eventType, string payload, DateTime occurredOn, Guid messageId)
    {
        if (messageId == Guid.Empty)
            throw new ArgumentException("MessageId (domain EventId) is required.", nameof(messageId));

        EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        OccurredOn = occurredOn;
        MessageId = messageId;
        Status = OutboxMessageStatus.Pending;
        NextAttemptAt = DateTime.UtcNow;
        ProcessedAt = null;
        DeadLetteredAt = null;
        Error = null;
        RetryCount = 0;
    }

    public bool IsDue(DateTime utcNow) =>
        Status is OutboxMessageStatus.Pending && NextAttemptAt <= utcNow;

    public void MarkProcessing(DateTime utcNow)
    {
        Status = OutboxMessageStatus.Processing;
        ProcessingStartedAt = utcNow;
        Error = null;
    }

    public void MarkProcessed(DateTime utcNow)
    {
        Status = OutboxMessageStatus.Processed;
        ProcessedAt = utcNow;
        ProcessingStartedAt = null;
        Error = null;
    }

    /// <summary>
    /// Schedules another attempt with exponential backoff, or dead-letters when max retries exhausted
    /// / non-retryable.
    /// </summary>
    public void MarkFailed(
        Exception exception,
        int maxRetryAttempts,
        TimeSpan baseDelay,
        TimeSpan maxDelay,
        bool nonRetryable = false)
    {
        var message = $"{exception.GetType().Name}: {exception.Message}";
        if (message.Length > 2000)
            message = message[..2000];

        Error = message;
        ProcessingStartedAt = null;

        if (nonRetryable)
        {
            MoveToDeadLetter(DateTime.UtcNow);
            RetryCount = Math.Max(RetryCount, maxRetryAttempts);
            return;
        }

        RetryCount++;

        if (RetryCount >= maxRetryAttempts)
        {
            MoveToDeadLetter(DateTime.UtcNow);
            return;
        }

        Status = OutboxMessageStatus.Pending;
        var exponential = Math.Pow(2, Math.Min(RetryCount - 1, 10));
        var delay = TimeSpan.FromMilliseconds(
            Math.Min(baseDelay.TotalMilliseconds * exponential, maxDelay.TotalMilliseconds));
        // Full jitter: [0, delay]
        var jitterMs = Random.Shared.NextDouble() * delay.TotalMilliseconds;
        NextAttemptAt = DateTime.UtcNow.AddMilliseconds(jitterMs);
    }

    public void MoveToDeadLetter(DateTime utcNow)
    {
        Status = OutboxMessageStatus.DeadLetter;
        DeadLetteredAt = utcNow;
        ProcessingStartedAt = null;
        // Stop scheduling
        NextAttemptAt = DateTime.MaxValue;
    }

    /// <summary>
    /// Releases a stuck Processing claim back to Pending (lease timeout recovery).
    /// </summary>
    public void ReleaseStaleClaim(DateTime utcNow, TimeSpan leaseTimeout)
    {
        if (Status != OutboxMessageStatus.Processing || ProcessingStartedAt is null)
            return;

        if (utcNow - ProcessingStartedAt.Value < leaseTimeout)
            return;

        Status = OutboxMessageStatus.Pending;
        ProcessingStartedAt = null;
        NextAttemptAt = utcNow;
        Error = "Released stale processing claim (lease timeout).";
    }
}
