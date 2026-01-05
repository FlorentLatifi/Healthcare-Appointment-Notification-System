using Healthcare.Application.Common;
using Healthcare.Application.DTOs;

namespace Healthcare.Application.Queries.GetAppointment;

/// <summary>
/// Query to get a single appointment by ID.
/// </summary>
/// <remarks>
/// Design Pattern: Query Pattern + CQRS
/// 
/// Queries are read-only operations that return data without modifying state.
/// They are optimized for reading and can bypass domain entities if needed.
/// </remarks>
public sealed class GetAppointmentQuery : IQuery<Result<AppointmentDto>>
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