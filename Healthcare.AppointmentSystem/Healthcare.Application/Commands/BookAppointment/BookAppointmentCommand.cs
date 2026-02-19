using Healthcare.Application.Common;
using Healthcare.Domain.Enums;

namespace Healthcare.Application.Commands.BookAppointment;

/// <summary>
/// Command to book a new appointment.
/// </summary>
/// <remarks>
/// Design Pattern: Command Pattern + CQRS
/// 
/// IMPORTANT: Must implement ICommand of Result of int  
/// so ICommandHandler constraint is satisfied.
/// </remarks>
public sealed class BookAppointmentCommand : ICommand<Result<int>>
{
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public DateTime ScheduledTime { get; set; }
    public string Reason { get; set; } = string.Empty;

    // Strategy Pattern: tells handler which pricing to apply
    public AppointmentType AppointmentType { get; set; } = AppointmentType.Standard;
}