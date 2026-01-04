using Healthcare.Domain.Common;

namespace Healthcare.Domain.Events;

/// <summary>
/// Domain event raised when an appointment is cancelled.
/// </summary>
public sealed class AppointmentCancelledEvent : IDomainEvent
{
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }

    public int AppointmentId { get; }
    public int PatientId { get; }
    public int DoctorId { get; }
    public DateTime ScheduledTime { get; }
    public string CancellationReason { get; }

    public AppointmentCancelledEvent(
        int appointmentId,
        int patientId,
        int doctorId,
        DateTime scheduledTime,
        string cancellationReason)
    {
        EventId = Guid.NewGuid();
        OccurredOn = DateTime.UtcNow;
        AppointmentId = appointmentId;
        PatientId = patientId;
        DoctorId = doctorId;
        ScheduledTime = scheduledTime;
        CancellationReason = cancellationReason;
    }
}