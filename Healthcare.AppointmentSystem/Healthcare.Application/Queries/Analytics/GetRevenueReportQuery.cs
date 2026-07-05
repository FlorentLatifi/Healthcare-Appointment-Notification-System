using Healthcare.Application.Common;
using Healthcare.Application.DTOs;

namespace Healthcare.Application.Queries.Analytics;

public sealed class GetRevenueReportQuery : IQuery<Result<RevenueReportDto>>
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public string? GroupBy { get; set; }

    public GetRevenueReportQuery(DateTime dateFrom, DateTime dateTo, string? groupBy = null)
    {
        DateFrom = dateFrom;
        DateTo = dateTo;
        GroupBy = groupBy;
    }
}
