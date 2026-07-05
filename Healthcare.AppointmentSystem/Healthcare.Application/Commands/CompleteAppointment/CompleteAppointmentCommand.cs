using Healthcare.Application.Common;

namespace Healthcare.Application.Commands.CompleteAppointment;

public sealed class CompleteAppointmentCommand : ICommand<Result>
{
    public int AppointmentId { get; set; }
    public string DoctorNotes { get; set; } = string.Empty;
}
