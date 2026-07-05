using Healthcare.Application.Common;
using Healthcare.Application.DTOs;

namespace Healthcare.Application.Queries.Analytics;

public sealed class GetAppointmentVolumeQuery : IQuery<Result<AppointmentVolumeDto>>
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public string GroupBy { get; set; }

    public GetAppointmentVolumeQuery(DateTime dateFrom, DateTime dateTo, string groupBy = "day")
    {
        DateFrom = dateFrom;
        DateTo = dateTo;
        GroupBy = groupBy;
    }
}
