using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Audit;
using Healthcare.Domain.Audit;
using MediatR;

namespace Healthcare.Application.Queries.GetAppointment;

/// <summary>
/// Query to get a single appointment by ID (MediatR + legacy IQuery during migration).
/// Automatically audited (PHI / payment adjacency).
/// </summary>
public sealed class GetAppointmentQuery : IRequest<Result<AppointmentDto>>, IQuery<Result<AppointmentDto>>, IAuditableRequest
{
    /// <summary>
    /// Gets or sets the appointment ID.
    /// </summary>
    public int AppointmentId { get; set; }

    public GetAppointmentQuery(int appointmentId)
    {
        AppointmentId = appointmentId;
    }

    string IAuditableRequest.AuditAction => AuditActions.GetAppointment;
    string IAuditableRequest.AuditResourceType => "Appointment";
    int? IAuditableRequest.AuditResourceId => AppointmentId;
    object IAuditableRequest.GetAuditDetails() => new { AppointmentId };
    int? IAuditableRequest.ResolveResourceId(object? response) => AppointmentId;
}