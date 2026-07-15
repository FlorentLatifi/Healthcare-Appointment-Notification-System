using Asp.Versioning;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Presentation.API.Authorization;
using Healthcare.Presentation.API.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Healthcare.Presentation.API.Controllers;

/// <summary>
/// In-app notification inbox for the authenticated user (Patient, Doctor, Admin).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize(Roles = AppRoles.PatientOrDoctorOrAdmin)]
public sealed class NotificationsController : ControllerBase
{
    private readonly IUserNotificationRepository _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        IUserNotificationRepository notifications,
        IUnitOfWork unitOfWork,
        ILogger<NotificationsController> logger)
    {
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>Paged list of the current user's notifications (newest first).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var items = await _notifications.GetByUserIdAsync(userId, pageNumber, pageSize, cancellationToken);
        var totalCount = await _notifications.CountByUserIdAsync(userId, cancellationToken);
        var unreadCount = await _notifications.CountUnreadByUserIdAsync(userId, cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var dtos = items.Select(MapToDto).ToList();
        var payload = new
        {
            items = dtos,
            pageNumber,
            pageSize,
            totalCount,
            totalPages,
            unreadCount,
            hasPreviousPage = pageNumber > 1,
            hasNextPage = pageNumber < totalPages
        };

        return Ok(ApiResponse<object>.SuccessResponse(payload));
    }

    /// <summary>Unread count for navbar badge.</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var count = await _notifications.CountUnreadByUserIdAsync(userId, cancellationToken);
        return Ok(ApiResponse<object>.SuccessResponse(new { count }));
    }

    /// <summary>Mark a single notification as read (owner only).</summary>
    [HttpPut("{id:int}/read")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var note = await _notifications.GetByIdAsync(id, cancellationToken);
        if (note is null)
            return NotFound(ApiResponse.ErrorResponse($"Notification {id} not found", "Not found"));
        if (note.UserId != userId)
            return Forbid();

        note.MarkAsRead();
        await _notifications.UpdateAsync(note, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} marked notification {NotificationId} as read", userId, id);
        return Ok(ApiResponse.SuccessResponse("Notification marked as read"));
    }

    /// <summary>Mark all of the current user's notifications as read.</summary>
    [HttpPut("read-all")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await _notifications.MarkAllReadAsync(userId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse.SuccessResponse("All notifications marked as read"));
    }

    private static UserNotificationDto MapToDto(Domain.Entities.UserNotification n) => new()
    {
        Id = n.Id,
        Title = n.Title,
        Message = n.Message,
        IsRead = n.IsRead,
        Category = n.Category,
        RelatedEntityType = n.RelatedEntityType,
        RelatedEntityId = n.RelatedEntityId,
        CreatedAt = n.CreatedAt,
        ReadAt = n.ReadAt
    };
}
