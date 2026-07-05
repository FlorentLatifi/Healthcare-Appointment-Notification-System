using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Repositories;

namespace Healthcare.Application.Queries.Analytics;

public sealed class GetNoShowRateHandler : IQueryHandler<GetNoShowRateQuery, Result<NoShowRateDto>>
{
    private readonly IAppointmentRepository _appointmentRepository;

    public GetNoShowRateHandler(IAppointmentRepository appointmentRepository)
    {
        _appointmentRepository = appointmentRepository;
    }

    public async Task<Result<NoShowRateDto>> HandleAsync(
        GetNoShowRateQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var counts = await _appointmentRepository.GetStatusCountsAsync(
                query.DateFrom, query.DateTo, cancellationToken);

            var denominator = counts.Confirmed + counts.Completed + counts.NoShow;
            var rate = denominator > 0
                ? (double)counts.NoShow / denominator * 100.0
                : 0.0;

            var dto = new NoShowRateDto
            {
                DateFrom = query.DateFrom,
                DateTo = query.DateTo,
                NoShowRatePercent = Math.Round(rate, 2),
                ConfirmedCount = counts.Confirmed,
                CompletedCount = counts.Completed,
                NoShowCount = counts.NoShow,
                TotalCount = counts.Confirmed + counts.Completed + counts.NoShow
            };

            return Result<NoShowRateDto>.Success(dto);
        }
        catch (Exception ex)
        {
            return Result<NoShowRateDto>.Failure(
                $"An unexpected error occurred: {ex.Message}");
        }
    }
}
