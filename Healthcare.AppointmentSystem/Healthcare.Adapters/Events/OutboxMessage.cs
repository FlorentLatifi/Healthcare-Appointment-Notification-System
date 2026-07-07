namespace Healthcare.Adapters.Events;

public sealed class OutboxMessage
{
    public int Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime OccurredOn { get; private set; }
    public DateTime? ProcessedAt { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }

    private OutboxMessage() { }

    public OutboxMessage(string eventType, string payload, DateTime occurredOn)
    {
        EventType = eventType;
        Payload = payload;
        OccurredOn = occurredOn;
        ProcessedAt = null;
        Error = null;
        RetryCount = 0;
    }
}
