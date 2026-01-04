using Healthcare.Domain.Common;

namespace Healthcare.Domain.Events;

/// <summary>
/// Domain event raised when a new appointment is created.
/// </summary>
public sealed class AppointmentCreatedEvent : IDomainEvent
{
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }

    public int AppointmentId { get; }
    public int PatientId { get; }
    public int DoctorId { get; }
    public DateTime ScheduledTime { get; }

    public AppointmentCreatedEvent(
        int appointmentId,
        int patientId,
        int doctorId,
        DateTime scheduledTime)
    {
        EventId = Guid.NewGuid();
        OccurredOn = DateTime.UtcNow;
        AppointmentId = appointmentId;
        PatientId = patientId;
        DoctorId = doctorId;
        ScheduledTime = scheduledTime;
    }
}