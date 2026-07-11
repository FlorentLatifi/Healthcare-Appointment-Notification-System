namespace Healthcare.Adapters.Background;

public sealed class OutboxRelayHealthState : WorkerHealthState
{
    public const string Name = "outbox-relay";

    public OutboxRelayHealthState() : base(Name)
    {
    }
}
