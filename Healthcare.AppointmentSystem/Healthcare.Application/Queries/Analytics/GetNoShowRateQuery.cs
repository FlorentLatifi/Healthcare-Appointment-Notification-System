using Healthcare.Application.Common;
using Healthcare.Application.DTOs;

namespace Healthcare.Application.Queries.Analytics;

public sealed class GetNoShowRateQuery : IQuery<Result<NoShowRateDto>>
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }

    public GetNoShowRateQuery(DateTime dateFrom, DateTime dateTo)
    {
        DateFrom = dateFrom;
        DateTo = dateTo;
    }
}
