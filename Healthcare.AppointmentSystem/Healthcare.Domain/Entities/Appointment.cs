using Healthcare.Domain.Common;
using Healthcare.Domain.Enums;
using Healthcare.Domain.Events;
using Healthcare.Domain.ValueObjects;

namespace Healthcare.Domain.Entities;

/// <summary>
/// Represents a medical appointment between a patient and a doctor.
/// This is an Aggregate Root in DDD terms.
/// </summary>
/// <remarks>
/// Design Pattern: Aggregate Root + State Pattern (status transitions)
/// 
/// State transition rules:
/// - Pending → Confirmed, Cancelled
/// - Confirmed → Completed, Cancelled, NoShow
/// - Completed, Cancelled, NoShow → Terminal (no further changes)
/// 
/// All state changes raise domain events for the Observer pattern.
/// </remarks>
public sealed class Appointment : Entity
{
    /// <summary>
    /// Gets the patient ID associated with this appointment.
    /// </summary>
    public int PatientId { get; private set; }

    /// <summary>
    /// Gets the patient navigation property (for EF Core).
    /// </summary>
    public Patient Patient { get; private set; } = null!;

    /// <summary>
    /// Gets the doctor ID associated with this appointment.
    /// </summary>
    public int DoctorId { get; private set; }

    /// <summary>
    /// Gets the doctor navigation property (for EF Core).
    /// </summary>
    public Doctor Doctor { get; private set; } = null!;

    /// <summary>
    /// Gets the scheduled time for this appointment.
    /// </summary>
    public AppointmentTime ScheduledTime { get; private set; } = null!;

    /// <summary>
    /// Gets the current status of this appointment.
    /// </summary>
    public AppointmentStatus Status { get; private set; }

    /// <summary>
    /// Gets the reason for the appointment.
    /// </summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the notes added by the doctor during/after the appointment.
    /// </summary>
    public string? DoctorNotes { get; private set; }

    /// <summary>
    /// Gets the cancellation reason (if appointment was cancelled).
    /// </summary>
    public string? CancellationReason { get; private set; }

    /// <summary>
    /// Gets the date and time when the appointment was confirmed.
    /// </summary>
    public DateTime? ConfirmedAt { get; private set; }

    /// <summary>
    /// Gets the date and time when the appointment was completed.
    /// </summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// Gets the date and time when the appointment was cancelled.
    /// </summary>
    public DateTime? CancelledAt { get; private set; }

    /// <summary>
    /// Gets the consultation fee for this appointment.
    /// </summary>
    public Money ConsultationFee { get; private set; } = null!;

    // Private parameterless constructor for EF Core
    private Appointment() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Appointment"/> class.
    /// Private constructor - use Create() factory method.
    /// </summary>
    private Appointment(
        int patientId,
        int doctorId,
        AppointmentTime scheduledTime,
        string reason,
        Money consultationFee)
    {
        PatientId = patientId;
        DoctorId = doctorId;
        ScheduledTime = scheduledTime;
        Reason = reason;
        ConsultationFee = consultationFee;
        Status = AppointmentStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a new Appointment using the Factory Method pattern.
    /// </summary>
    /// <param name="patient">The patient booking the appointment.</param>
    /// <param name="doctor">The doctor for the appointment.</param>
    /// <param name="scheduledTime">The desired appointment time.</param>
    /// <param name="reason">The reason for the appointment.</param>
    /// <returns>A new valid Appointment entity.</returns>
    public static Appointment Create(
        Patient patient,
        Doctor doctor,
        AppointmentTime scheduledTime,
        string reason)
    {
        Guard.AgainstNull(patient, nameof(patient));
        Guard.AgainstNull(doctor, nameof(doctor));
        Guard.AgainstNull(scheduledTime, nameof(scheduledTime));
        Guard.AgainstNullOrWhiteSpace(reason, nameof(reason));

        if (!patient.IsActive)
        {
            throw new InvalidOperationException("Cannot book appointment - patient account is inactive.");
        }

        if (!doctor.IsActive)
        {
            throw new InvalidOperationException("Cannot book appointment - doctor is not active.");
        }

        if (!doctor.IsAcceptingPatients)
        {
            throw new InvalidOperationException("Cannot book appointment - doctor is not accepting patients.");
        }

        if (reason.Trim().Length < 10)
        {
            throw new ArgumentException("Appointment reason must be at least 10 characters.", nameof(reason));
        }

        var appointment = new Appointment(
            patient.Id,
            doctor.Id,
            scheduledTime,
            reason.Trim(),
            doctor.ConsultationFee);

        appointment.AddDomainEvent(new AppointmentCreatedEvent(
            appointment.Id,
            appointment.PatientId,
            appointment.DoctorId,
            appointment.ScheduledTime.Value));

        return appointment;
    }

    /// <summary>
    /// Confirms the appointment.
    /// </summary>
    public void Confirm()
    {
        if (Status != AppointmentStatus.Pending)
        {
            throw new InvalidAppointmentStateException("confirm", Status.ToString());
        }

        if (ScheduledTime.IsPast())
        {
            throw new InvalidOperationException("Cannot confirm past appointments.");
        }

        Status = AppointmentStatus.Confirmed;
        ConfirmedAt = DateTime.UtcNow;
        MarkAsModified();

        AddDomainEvent(new AppointmentConfirmedEvent(
            Id,
            PatientId,
            DoctorId,
            ScheduledTime.Value));
    }

    /// <summary>
    /// Cancels the appointment.
    /// </summary>
    /// <param name="cancellationReason">The reason for cancellation.</param>
    public void Cancel(string cancellationReason)
    {
        Guard.AgainstNullOrWhiteSpace(cancellationReason, nameof(cancellationReason));

        if (Status != AppointmentStatus.Pending && Status != AppointmentStatus.Confirmed)
        {
            throw new InvalidAppointmentStateException("cancel", Status.ToString());
        }

        if (cancellationReason.Trim().Length < 10)
        {
            throw new ArgumentException("Cancellation reason must be at least 10 characters.", nameof(cancellationReason));
        }

        Status = AppointmentStatus.Cancelled;
        CancellationReason = cancellationReason.Trim();
        CancelledAt = DateTime.UtcNow;
        MarkAsModified();

        AddDomainEvent(new AppointmentCancelledEvent(
            Id,
            PatientId,
            DoctorId,
            ScheduledTime.Value,
            CancellationReason));
    }

    /// <summary>
    /// Marks the appointment as completed with doctor's notes.
    /// </summary>
    /// <param name="doctorNotes">Notes added by the doctor.</param>
    public void Complete(string doctorNotes)
    {
        Guard.AgainstNullOrWhiteSpace(doctorNotes, nameof(doctorNotes));

        if (Status != AppointmentStatus.Confirmed)
        {
            throw new InvalidAppointmentStateException("complete", Status.ToString());
        }

        if (doctorNotes.Trim().Length < 20)
        {
            throw new ArgumentException("Doctor notes must be at least 20 characters.", nameof(doctorNotes));
        }

        Status = AppointmentStatus.Completed;
        DoctorNotes = doctorNotes.Trim();
        CompletedAt = DateTime.UtcNow;
        MarkAsModified();

        AddDomainEvent(new AppointmentCompletedEvent(
            Id,
            PatientId,
            DoctorId,
            ScheduledTime.Value));
    }

    /// <summary>
    /// Marks the appointment as a no-show (patient didn't arrive).
    /// </summary>
    public void MarkAsNoShow()
    {
        if (Status != AppointmentStatus.Confirmed)
        {
            throw new InvalidAppointmentStateException("mark as no-show", Status.ToString());
        }

        if (!ScheduledTime.IsPast())
        {
            throw new InvalidOperationException("Cannot mark as no-show before the scheduled time.");
        }

        Status = AppointmentStatus.NoShow;
        MarkAsModified();

        AddDomainEvent(new AppointmentNoShowEvent(
            Id,
            PatientId,
            DoctorId,
            ScheduledTime.Value));
    }

    /// <summary>
    /// Reschedules the appointment to a new time.
    /// </summary>
    /// <param name="newScheduledTime">The new appointment time.</param>
    public void Reschedule(AppointmentTime newScheduledTime)
    {
        Guard.AgainstNull(newScheduledTime, nameof(newScheduledTime));

        if (Status != AppointmentStatus.Pending && Status != AppointmentStatus.Confirmed)
        {
            throw new InvalidAppointmentStateException("reschedule", Status.ToString());
        }

        if (newScheduledTime == ScheduledTime)
        {
            throw new InvalidOperationException("New appointment time must be different from current time.");
        }

        ScheduledTime = newScheduledTime;
        MarkAsModified();
    }

    /// <summary>
    /// Checks if the appointment needs a reminder (within 24 hours).
    /// </summary>
    public bool NeedsReminder()
    {
        return Status == AppointmentStatus.Confirmed && ScheduledTime.IsWithinNext24Hours();
    }

    /// <summary>
    /// Checks if the appointment is terminal (no more state changes allowed).
    /// </summary>
    public bool IsTerminal()
    {
        return Status == AppointmentStatus.Completed ||
               Status == AppointmentStatus.Cancelled ||
               Status == AppointmentStatus.NoShow;
    }
}