using Asp.Versioning;
using Healthcare.Application.Ports.Authentication;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Presentation.API.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Healthcare.Presentation.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/sessions")]
[Authorize]
[Produces("application/json")]
public sealed class SessionsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthenticationService _authService;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(
        IUnitOfWork unitOfWork,
        IAuthenticationService authService,
        ILogger<SessionsController> logger)
    {
        _unitOfWork = unitOfWork;
        _authService = authService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<object>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var sessions = await _unitOfWork.UserSessions.GetActiveByUserIdAsync(userId, cancellationToken);

        var result = sessions.Select(s => new
        {
            s.Id,
            s.FamilyId,
            s.LastUsedAt,
            s.UserAgent,
            s.IpAddress,
            s.CreatedAt
        }).ToList();

        return Ok(ApiResponse<List<object>>.SuccessResponse(
            result.Cast<object>().ToList(),
            "Active sessions retrieved"));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeSession(int id, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var session = await _unitOfWork.UserSessions.GetByIdAsync(id, cancellationToken);
        if (session == null || session.UserId != userId)
        {
            return BadRequest(ApiResponse.ErrorResponse(
                "Session not found.",
                "Failed to revoke session"));
        }

        if (session.IsRevoked)
        {
            return BadRequest(ApiResponse.ErrorResponse(
                "Session is already revoked.",
                "Failed to revoke session"));
        }

        await _authService.RevokeFamilyAsync(session.FamilyId, cancellationToken);

        session.Revoke();
        await _unitOfWork.UserSessions.UpdateAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Session {SessionId} revoked for user {UserId}", id, userId);

        return Ok(ApiResponse.SuccessResponse("Session revoked successfully. You will need to login again on that device."));
    }

    [HttpDelete]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeAllSessions(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        await _authService.RevokeAllUserSessionsAsync(userId, cancellationToken);

        _logger.LogInformation("All sessions revoked for user {UserId}", userId);

        return Ok(ApiResponse.SuccessResponse("All sessions revoked. You will need to login again on all devices."));
    }
}
