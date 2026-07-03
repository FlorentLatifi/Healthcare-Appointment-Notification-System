using Healthcare.Domain.Common;

namespace Healthcare.Domain.Events;

/// <summary>
/// Domain event raised when an appointment is confirmed.
/// </summary>
public sealed class AppointmentConfirmedEvent : IDomainEvent
{
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }

    public int AppointmentId { get; }
    public int PatientId { get; }
    public int DoctorId { get; }
    public DateTime ScheduledTime { get; }

    /// <summary>
    /// Non-null when this appointment was confirmed by a Doctor/Admin who
    /// explicitly overrode the "must be paid before confirmation" business
    /// rule. Null for the normal path (payment already succeeded, or the
    /// rule didn't apply because the appointment wasn't Pending).
    /// </summary>
    public string? PaymentOverrideReason { get; }

    public AppointmentConfirmedEvent(
        int appointmentId,
        int patientId,
        int doctorId,
        DateTime scheduledTime,
        string? paymentOverrideReason = null)
    {
        EventId = Guid.NewGuid();
        OccurredOn = DateTime.UtcNow;
        AppointmentId = appointmentId;
        PatientId = patientId;
        DoctorId = doctorId;
        ScheduledTime = scheduledTime;
        PaymentOverrideReason = paymentOverrideReason;
    }
}