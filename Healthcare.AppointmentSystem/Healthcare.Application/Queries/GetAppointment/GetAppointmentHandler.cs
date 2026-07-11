using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Mappings;
using Healthcare.Application.Ports.Repositories;
using MediatR;

namespace Healthcare.Application.Queries.GetAppointment;

public sealed class GetAppointmentHandler
    : IRequestHandler<GetAppointmentQuery, Result<AppointmentDto>>,
      IQueryHandler<GetAppointmentQuery, Result<AppointmentDto>>
{
    private readonly IAppointmentRepository _appointmentRepository;

    public GetAppointmentHandler(IAppointmentRepository appointmentRepository)
    {
        _appointmentRepository = appointmentRepository;
    }

    public Task<Result<AppointmentDto>> Handle(GetAppointmentQuery request, CancellationToken cancellationToken)
        => HandleAsync(request, cancellationToken);

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