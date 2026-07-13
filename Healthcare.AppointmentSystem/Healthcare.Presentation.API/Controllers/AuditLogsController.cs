using Asp.Versioning;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Presentation.API.Authorization;
using Healthcare.Presentation.API.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Healthcare.Presentation.API.Controllers;

/// <summary>
/// Admin-only query API for immutable audit logs (HIPAA accountability).
/// </summary>
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

    /// <summary>
    /// Query audit logs with pagination and filters (action, actor, resource, outcome, time range).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? entityType = null,
        [FromQuery] string? resourceType = null,
        [FromQuery] int? entityId = null,
        [FromQuery] int? resourceId = null,
        [FromQuery] string? action = null,
        [FromQuery] int? actorUserId = null,
        [FromQuery] string? outcome = null,
        [FromQuery] string? correlationId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var resolvedType = resourceType ?? entityType;
        var resolvedId = resourceId ?? entityId;

        _logger.LogInformation(
            "Retrieving audit logs - ResourceType: {ResourceType}, ResourceId: {ResourceId}, Action: {Action}, " +
            "Actor: {Actor}, Outcome: {Outcome}, From: {From}, To: {To}, Page: {Page}, Size: {Size}",
            resolvedType, resolvedId, action, actorUserId, outcome, from, to, pageNumber, pageSize);

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var entries = await _auditLogRepo.QueryAsync(
            resolvedType, resolvedId, from, to, pageNumber, pageSize,
            action, actorUserId, outcome, correlationId, cancellationToken);

        var totalCount = await _auditLogRepo.CountAsync(
            resolvedType, resolvedId, from, to,
            action, actorUserId, outcome, correlationId, cancellationToken);

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

    [HttpGet("{id:int}")]
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
            Action = entry.EventType,
            EventType = entry.EventType,
            ResourceType = entry.EntityType,
            EntityType = entry.EntityType,
            ResourceId = entry.EntityId,
            EntityId = entry.EntityId,
            OccurredOn = entry.OccurredOn,
            Details = entry.Details,
            ActorUserId = entry.UserId,
            UserId = entry.UserId,
            ActorRole = entry.ActorRole,
            Outcome = entry.Outcome,
            ClientIp = entry.ClientIp,
            CorrelationId = entry.CorrelationId,
            UserAgent = entry.UserAgent,
            CreatedAt = entry.CreatedAt
        };
    }
}
