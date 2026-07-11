using Healthcare.Application.Common;
using Healthcare.Domain.Enums;
using MediatR;

namespace Healthcare.Application.Commands.BookAppointment;

/// <summary>
/// Command to book a new appointment (MediatR + legacy ICommand during migration).
/// </summary>
public sealed class BookAppointmentCommand : IRequest<Result<int>>, ICommand<Result<int>>, ITransactionalRequest
{
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public DateTime ScheduledTime { get; set; }
    public string Reason { get; set; } = string.Empty;
    public AppointmentType AppointmentType { get; set; } = AppointmentType.Standard;
}