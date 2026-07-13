using Healthcare.Application.Common;
using Healthcare.Application.Ports.Audit;
using Healthcare.Domain.Audit;
using Healthcare.Domain.Enums;
using MediatR;

namespace Healthcare.Application.Commands.BookAppointment;

/// <summary>
/// Command to book a new appointment (MediatR + legacy ICommand during migration).
/// Automatically audited via <see cref="IAuditableRequest"/>.
/// </summary>
public sealed class BookAppointmentCommand : IRequest<Result<int>>, ICommand<Result<int>>, ITransactionalRequest, IAuditableRequest
{
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public DateTime ScheduledTime { get; set; }
    public string Reason { get; set; } = string.Empty;
    public AppointmentType AppointmentType { get; set; } = AppointmentType.Standard;

    string IAuditableRequest.AuditAction => AuditActions.BookAppointment;
    string IAuditableRequest.AuditResourceType => "Appointment";
    int? IAuditableRequest.AuditResourceId => null;

    object IAuditableRequest.GetAuditDetails() => new
    {
        PatientId,
        DoctorId,
        ScheduledTimeUtc = ScheduledTime,
        AppointmentType = AppointmentType.ToString()
        // Reason intentionally omitted — free-text may contain clinical PHI
    };

    int? IAuditableRequest.ResolveResourceId(object? response)
    {
        if (response is Result<int> r && r.IsSuccess)
            return r.Value;
        return null;
    }
}