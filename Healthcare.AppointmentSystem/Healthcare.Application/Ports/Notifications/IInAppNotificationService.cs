namespace Healthcare.Application.Ports.Notifications;

/// <summary>
/// Writes in-app inbox notifications for users (distinct from email/SMS <see cref="INotificationService"/>).
/// </summary>
public interface IInAppNotificationService
{
    Task NotifyUserAsync(
        int userId,
        string title,
        string message,
        string? category = null,
        string? relatedEntityType = null,
        int? relatedEntityId = null,
        CancellationToken cancellationToken = default);

    Task NotifyUsersLinkedToAppointmentAsync(
        int appointmentId,
        string title,
        string message,
        string? category = null,
        CancellationToken cancellationToken = default);
}
