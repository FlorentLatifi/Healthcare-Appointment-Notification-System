using Asp.Versioning;
using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Queries.Analytics;
using Healthcare.Presentation.API.Authorization;
using Healthcare.Presentation.API.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Healthcare.Presentation.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class AnalyticsController : ControllerBase
{
    private readonly IQueryHandler<GetRevenueReportQuery, Result<RevenueReportDto>> _revenueHandler;
    private readonly IQueryHandler<GetNoShowRateQuery, Result<NoShowRateDto>> _noShowHandler;
    private readonly IQueryHandler<GetAppointmentVolumeQuery, Result<AppointmentVolumeDto>> _volumeHandler;

    public AnalyticsController(
        IQueryHandler<GetRevenueReportQuery, Result<RevenueReportDto>> revenueHandler,
        IQueryHandler<GetNoShowRateQuery, Result<NoShowRateDto>> noShowHandler,
        IQueryHandler<GetAppointmentVolumeQuery, Result<AppointmentVolumeDto>> volumeHandler)
    {
        _revenueHandler = revenueHandler;
        _noShowHandler = noShowHandler;
        _volumeHandler = volumeHandler;
    }

    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenue(
        [FromQuery] DateTime dateFrom,
        [FromQuery] DateTime dateTo,
        [FromQuery] string? groupBy = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetRevenueReportQuery(dateFrom, dateTo, groupBy);
        var result = await _revenueHandler.HandleAsync(query, cancellationToken);

        if (!result.IsSuccess)
            return Ok(ApiResponse<RevenueReportDto>.ErrorResponse(result.Error!));

        return Ok(ApiResponse<RevenueReportDto>.SuccessResponse(result.Value!));
    }

    [HttpGet("no-show-rate")]
    public async Task<IActionResult> GetNoShowRate(
        [FromQuery] DateTime dateFrom,
        [FromQuery] DateTime dateTo,
        CancellationToken cancellationToken = default)
    {
        var query = new GetNoShowRateQuery(dateFrom, dateTo);
        var result = await _noShowHandler.HandleAsync(query, cancellationToken);

        if (!result.IsSuccess)
            return Ok(ApiResponse<NoShowRateDto>.ErrorResponse(result.Error!));

        return Ok(ApiResponse<NoShowRateDto>.SuccessResponse(result.Value!));
    }

    [HttpGet("volume")]
    public async Task<IActionResult> GetVolume(
        [FromQuery] DateTime dateFrom,
        [FromQuery] DateTime dateTo,
        [FromQuery] string groupBy = "day",
        CancellationToken cancellationToken = default)
    {
        var query = new GetAppointmentVolumeQuery(dateFrom, dateTo, groupBy);
        var result = await _volumeHandler.HandleAsync(query, cancellationToken);

        if (!result.IsSuccess)
            return Ok(ApiResponse<AppointmentVolumeDto>.ErrorResponse(result.Error!));

        return Ok(ApiResponse<AppointmentVolumeDto>.SuccessResponse(result.Value!));
    }
}
