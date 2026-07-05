using Asp.Versioning;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Repositories;
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
public sealed class AuditLogsController : ControllerBase
{
    private readonly IAuditLogRepository _auditLogRepo;
    private readonly ILogger<AuditLogsController> _logger;

    public AuditLogsController(
        IAuditLogRepository auditLogRepo,
        ILogger<AuditLogsController> logger)
    {
        _auditLogRepo = auditLogRepo;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<AuditLogDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? entityType = null,
        [FromQuery] int? entityId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Retrieving audit logs - EntityType: {EntityType}, EntityId: {EntityId}, " +
            "From: {From}, To: {To}, Page: {Page}, Size: {Size}",
            entityType, entityId, from, to, pageNumber, pageSize);

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var entries = await _auditLogRepo.QueryAsync(
            entityType, entityId, from, to, pageNumber, pageSize, cancellationToken);

        var totalCount = await _auditLogRepo.CountAsync(
            entityType, entityId, from, to, cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var dtos = entries.Select(MapToDto).ToList();

        var pagedResult = new
        {
            items = dtos,
            pageNumber,
            pageSize,
            totalCount,
            totalPages,
            hasPreviousPage = pageNumber > 1,
            hasNextPage = pageNumber < totalPages
        };

        return Ok(ApiResponse<object>.SuccessResponse(
            pagedResult,
            $"Retrieved page {pageNumber} of {totalPages} ({dtos.Count} log(s))"));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<AuditLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuditLogById(
        int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving audit log {AuditLogId}", id);

        var entry = await _auditLogRepo.GetByIdAsync(id, cancellationToken);
        if (entry == null)
        {
            return NotFound(ApiResponse.ErrorResponse(
                $"Audit log with ID {id} not found", "Audit log not found"));
        }

        return Ok(ApiResponse<AuditLogDto>.SuccessResponse(MapToDto(entry)));
    }

    private static AuditLogDto MapToDto(Domain.Entities.AuditLogEntry entry)
    {
        return new AuditLogDto
        {
            Id = entry.Id,
            EventType = entry.EventType,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            OccurredOn = entry.OccurredOn,
            Details = entry.Details,
            UserId = entry.UserId,
            CreatedAt = entry.CreatedAt
        };
    }
}
