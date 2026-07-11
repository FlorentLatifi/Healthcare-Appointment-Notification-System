namespace Healthcare.Adapters.Background;

public sealed class AppointmentReminderHealthState : WorkerHealthState
{
    public const string Name = "appointment-reminder";

    public AppointmentReminderHealthState() : base(Name)
    {
    }
}
