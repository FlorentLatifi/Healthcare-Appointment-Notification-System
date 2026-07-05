using Healthcare.Application.Common;

namespace Healthcare.Application.Commands.MarkNoShowAppointment;

public sealed class MarkNoShowAppointmentCommand : ICommand<Result>
{
    public int AppointmentId { get; set; }
}
