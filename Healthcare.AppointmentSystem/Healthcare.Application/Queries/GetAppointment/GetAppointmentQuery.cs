using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using MediatR;

namespace Healthcare.Application.Queries.GetAppointment;

/// <summary>
/// Query to get a single appointment by ID (MediatR + legacy IQuery during migration).
/// </summary>
public sealed class GetAppointmentQuery : IRequest<Result<AppointmentDto>>, IQuery<Result<AppointmentDto>>
{
    /// <summary>
    /// Gets or sets the appointment ID.
    /// </summary>
    public int AppointmentId { get; set; }

    public GetAppointmentQuery(int appointmentId)
    {
        AppointmentId = appointmentId;
    }
}