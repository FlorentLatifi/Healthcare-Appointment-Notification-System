using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Mappings;
using Healthcare.Application.Ports.Repositories;

namespace Healthcare.Application.Queries.GetAppointment;

public sealed class GetAppointmentHandler : IQueryHandler<GetAppointmentQuery, Result<AppointmentDto>>
{
    private readonly IAppointmentRepository _appointmentRepository;

    public GetAppointmentHandler(IAppointmentRepository appointmentRepository)
    {
        _appointmentRepository = appointmentRepository;
    }

    public async Task<Result<AppointmentDto>> HandleAsync(
        GetAppointmentQuery query,
        CancellationToken cancellationToken = default)
    {
        var appointment = await _appointmentRepository
            .GetByIdAsync(query.AppointmentId, cancellationToken);

        if (appointment is null)
            return Result<AppointmentDto>.Failure(
                $"Appointment with ID {query.AppointmentId} not found.");

        var dto = AppointmentMapper.ToDto(appointment);

        return Result<AppointmentDto>.Success(dto);
    }
}