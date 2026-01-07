namespace Healthcare.Presentation.API.Requests;

/// <summary>
/// Request model for booking a new appointment.
/// </summary>
/// <remarks>
/// Design Pattern: Data Transfer Object (DTO)
/// 
/// This DTO:
/// - Receives data from HTTP POST request body
/// - Validates input via FluentValidation
/// - Maps to BookAppointmentCommand
/// - Decouples API from Application layer
/// </remarks>
public sealed class BookAppointmentRequest
{
    /// <summary>
    /// Gets or sets the patient ID.
    /// </summary>
    /// <example>1</example>
    public int PatientId { get; set; }

    /// <summary>
    /// Gets or sets the doctor ID.
    /// </summary>
    /// <example>2</example>
    public int DoctorId { get; set; }

    /// <summary>
    /// Gets or sets the desired appointment date and time.
    /// </summary>
    /// <example>2025-01-20T14:00:00Z</example>
    public DateTime ScheduledTime { get; set; }

    /// <summary>
    /// Gets or sets the reason for the appointment.
    /// </summary>
    /// <example>Annual checkup and blood pressure monitoring</example>
    public string Reason { get; set; } = string.Empty;
}