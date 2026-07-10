using Asp.Versioning;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Presentation.API.Authorization;
using Healthcare.Presentation.API.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace Healthcare.Presentation.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public sealed class UsersController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUnitOfWork unitOfWork,
        ILogger<UsersController> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    [HttpPost("{id}/promote-to-admin")]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PromoteToAdmin(
        int id,
        CancellationToken cancellationToken)
    {
        var performerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var performerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

        var user = await _unitOfWork.Users.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            return BadRequest(ApiResponse.ErrorResponse(
                $"User with ID {id} not found.",
                "Promotion failed"));
        }

        if (user.Role == Healthcare.Domain.Enums.UserRole.Admin)
        {
            return BadRequest(ApiResponse.ErrorResponse(
                $"User '{user.Username}' is already an Admin.",
                "Promotion failed"));
        }

        var previousRole = user.Role.ToString();
        user.PromoteToAdmin();
        await _unitOfWork.Users.UpdateAsync(user, cancellationToken);

        var details = JsonSerializer.Serialize(new
        {
            PromotedUserId = id,
            PromotedUsername = user.Username,
            PreviousRole = previousRole,
            PerformedByUserId = performerIdClaim,
            PerformedByUsername = performerUsername
        });

        int? performerId = int.TryParse(performerIdClaim, out var pid) ? pid : null;
        var auditEntry = new Healthcare.Domain.Entities.AuditLogEntry(
            "UserPromotedToAdmin",
            "User",
            id,
            DateTime.UtcNow,
            details,
            performerId);

        await _unitOfWork.AuditLogs.AddAsync(auditEntry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User {PromotedUsername} (ID {PromotedUserId}) promoted to Admin by {PerformerUsername} (ID {PerformerId})",
            user.Username, id, performerUsername, performerId);

        return Ok(ApiResponse.SuccessResponse(
            $"User '{user.Username}' has been promoted to Admin."));
    }
}
