using Healthcare.Domain.Entities;

namespace Healthcare.Application.Ports.Repositories;

/// <summary>
/// In-app notifications inbox for authenticated users.
/// </summary>
public interface IUserNotificationRepository
{
    Task AddAsync(UserNotification notification, CancellationToken cancellationToken = default);

    Task<UserNotification?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserNotification>> GetByUserIdAsync(
        int userId,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<int> CountByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    Task<int> CountUnreadByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    Task UpdateAsync(UserNotification notification, CancellationToken cancellationToken = default);

    Task MarkAllReadAsync(int userId, CancellationToken cancellationToken = default);
}
