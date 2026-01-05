using Healthcare.Application.Common;

namespace Healthcare.Application.Commands.BookAppointment;

/// <summary>
/// Command to book a new appointment.
/// </summary>
/// <remarks>
/// Design Pattern: Command Pattern + CQRS
/// 
/// This command represents the intention to book an appointment.
/// It contains all the data needed to execute the operation.
/// </remarks>
public sealed class BookAppointmentCommand : ICommand<Result<int>>
{
    /// <summary>
    /// Gets or sets the patient ID.
    /// </summary>
    public int PatientId { get; set; }

    /// <summary>
    /// Gets or sets the doctor ID.
    /// </summary>
    public int DoctorId { get; set; }

    /// <summary>
    /// Gets or sets the desired appointment date and time.
    /// </summary>
    public DateTime ScheduledTime { get; set; }

    /// <summary>
    /// Gets or sets the reason for the appointment.
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}
