using Healthcare.Domain.Common;

namespace Healthcare.Domain.Entities;

/// <summary>
/// In-app notification for a signed-in user (patient, doctor, or admin).
/// </summary>
public sealed class UserNotification : Entity
{
    public int UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public bool IsRead { get; private set; }
    public string? Category { get; private set; }
    public string? RelatedEntityType { get; private set; }
    public int? RelatedEntityId { get; private set; }
    public DateTime? ReadAt { get; private set; }

    private UserNotification() { }

    private UserNotification(
        int userId,
        string title,
        string message,
        string? category,
        string? relatedEntityType,
        int? relatedEntityId)
    {
        UserId = userId;
        Title = title;
        Message = message;
        Category = category;
        RelatedEntityType = relatedEntityType;
        RelatedEntityId = relatedEntityId;
        IsRead = false;
        CreatedAt = DateTime.UtcNow;
    }

    public static UserNotification Create(
        int userId,
        string title,
        string message,
        string? category = null,
        string? relatedEntityType = null,
        int? relatedEntityId = null)
    {
        if (userId <= 0)
            throw new ArgumentException("User id must be positive.", nameof(userId));
        Guard.AgainstNullOrWhiteSpace(title, nameof(title));
        Guard.AgainstNullOrWhiteSpace(message, nameof(message));

        return new UserNotification(
            userId,
            title.Trim(),
            message.Trim(),
            string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            string.IsNullOrWhiteSpace(relatedEntityType) ? null : relatedEntityType.Trim(),
            relatedEntityId);
    }

    public void MarkAsRead()
    {
        if (IsRead) return;
        IsRead = true;
        ReadAt = DateTime.UtcNow;
        MarkAsModified();
    }
}
