using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;

namespace Healthcare.Adapters.Persistence.InMemory;

public sealed class InMemoryUserNotificationRepository : IUserNotificationRepository
{
    private readonly List<UserNotification> _items = new();
    private readonly object _lock = new();
    private int _nextId = 1;

    public Task AddAsync(UserNotification notification, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            typeof(UserNotification).GetProperty(nameof(UserNotification.Id))!
                .SetValue(notification, _nextId++);
            _items.Add(notification);
        }
        return Task.CompletedTask;
    }

    public Task<UserNotification?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_items.FirstOrDefault(n => n.Id == id));
        }
    }

    public Task<IReadOnlyList<UserNotification>> GetByUserIdAsync(
        int userId,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 20;
            var page = _items
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            return Task.FromResult<IReadOnlyList<UserNotification>>(page);
        }
    }

    public Task<int> CountByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_items.Count(n => n.UserId == userId));
        }
    }

    public Task<int> CountUnreadByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_items.Count(n => n.UserId == userId && !n.IsRead));
        }
    }

    public Task UpdateAsync(UserNotification notification, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task MarkAllReadAsync(int userId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            foreach (var n in _items.Where(x => x.UserId == userId && !x.IsRead))
                n.MarkAsRead();
        }
        return Task.CompletedTask;
    }
}
