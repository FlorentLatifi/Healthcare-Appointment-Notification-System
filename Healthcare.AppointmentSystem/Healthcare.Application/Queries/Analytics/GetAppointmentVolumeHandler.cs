using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Repositories;

namespace Healthcare.Application.Queries.Analytics;

public sealed class GetAppointmentVolumeHandler
    : IQueryHandler<GetAppointmentVolumeQuery, Result<AppointmentVolumeDto>>
{
    private readonly IAppointmentRepository _appointmentRepository;

    public GetAppointmentVolumeHandler(IAppointmentRepository appointmentRepository)
    {
        _appointmentRepository = appointmentRepository;
    }

    public async Task<Result<AppointmentVolumeDto>> HandleAsync(
        GetAppointmentVolumeQuery query,
        CancellationToken cancellationToken = default)
    {
        var dto = new AppointmentVolumeDto
        {
            DateFrom = query.DateFrom,
            DateTo = query.DateTo,
            GroupBy = query.GroupBy
        };

        if (string.Equals(query.GroupBy, "week", StringComparison.OrdinalIgnoreCase))
        {
            var weekly = await _appointmentRepository.GetWeeklyVolumeAsync(
                query.DateFrom, query.DateTo, cancellationToken);
            dto.Items = weekly
                .Select(r => new AppointmentVolumeItemDto
                {
                    Period = $"{r.Year}-W{r.Week:D2}",
                    Created = r.Created,
                    Confirmed = r.Confirmed,
                    Cancelled = r.Cancelled
                })
                .ToList();
        }
        else
        {
            var daily = await _appointmentRepository.GetDailyVolumeAsync(
                query.DateFrom, query.DateTo, cancellationToken);
            dto.Items = daily
                .Select(r => new AppointmentVolumeItemDto
                {
                    Period = r.Date.ToString("yyyy-MM-dd"),
                    Created = r.Created,
                    Confirmed = r.Confirmed,
                    Cancelled = r.Cancelled
                })
                .ToList();
        }

        return Result<AppointmentVolumeDto>.Success(dto);
    }
}
