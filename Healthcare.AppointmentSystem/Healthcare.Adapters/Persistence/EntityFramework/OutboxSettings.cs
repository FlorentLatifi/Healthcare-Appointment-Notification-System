namespace Healthcare.Adapters.Persistence.EntityFramework;

public class OutboxSettings
{
    public bool UseOutboxForDomainEvents { get; set; }
    public int RelayIntervalSeconds { get; set; } = 10;
    public int MaxRetryAttempts { get; set; } = 5;
    public int BatchSize { get; set; } = 50;
}
