using Healthcare.Domain.Common;

namespace Healthcare.Domain.Events;

/// <summary>
/// Domain event raised when a patient doesn't show up for a confirmed appointment.
/// </summary>
public sealed class AppointmentNoShowEvent : IDomainEvent
{
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }

    public int AppointmentId { get; }
    public int PatientId { get; }
    public int DoctorId { get; }
    public DateTime ScheduledTime { get; }

    public AppointmentNoShowEvent(
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