namespace Healthcare.Application.DTOs;

/// <summary>
/// Data Transfer Object for Appointment entity.
/// </summary>
public sealed class AppointmentDto
{
    /// <summary>
    /// Gets or sets the appointment ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the patient information.
    /// </summary>
    public PatientDto Patient { get; set; } = null!;

    /// <summary>
    /// Gets or sets the doctor information.
    /// </summary>
    public DoctorDto Doctor { get; set; } = null!;

    /// <summary>
    /// Gets or sets the scheduled date and time.
    /// </summary>
    public DateTime ScheduledTime { get; set; }

    /// <summary>
    /// Gets or sets the scheduled date (formatted).
    /// </summary>
    public string ScheduledDate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the scheduled time (formatted).
    /// </summary>
    public string ScheduledTimeFormatted { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the appointment status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the reason for the appointment.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the doctor's notes (if appointment is completed).
    /// </summary>
    public string? DoctorNotes { get; set; }

    /// <summary>
    /// Gets or sets the cancellation reason (if cancelled).
    /// </summary>
    public string? CancellationReason { get; set; }

    /// <summary>
    /// Gets or sets the consultation fee amount.
    /// </summary>
    public decimal ConsultationFeeAmount { get; set; }

    /// <summary>
    /// Gets or sets the consultation fee currency.
    /// </summary>
    public string ConsultationFeeCurrency { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the appointment was confirmed.
    /// </summary>
    public DateTime? ConfirmedAt { get; set; }

    /// <summary>
    /// Gets or sets when the appointment was completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets when the appointment was cancelled.
    /// </summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>
    /// Gets or sets when the appointment was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
