using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.Adapters.Persistence.EntityFramework.Repositories;

public sealed class EFCoreUserNotificationRepository : IUserNotificationRepository
{
    private readonly HealthcareDbContext _context;

    public EFCoreUserNotificationRepository(HealthcareDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(UserNotification notification, CancellationToken cancellationToken = default)
    {
        await _context.UserNotifications.AddAsync(notification, cancellationToken);
    }

    public async Task<UserNotification?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.UserNotifications
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<UserNotification>> GetByUserIdAsync(
        int userId,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        return await _context.UserNotifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _context.UserNotifications.CountAsync(n => n.UserId == userId, cancellationToken);
    }

    public async Task<int> CountUnreadByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _context.UserNotifications.CountAsync(
            n => n.UserId == userId && !n.IsRead, cancellationToken);
    }

    public Task UpdateAsync(UserNotification notification, CancellationToken cancellationToken = default)
    {
        _context.UserNotifications.Update(notification);
        return Task.CompletedTask;
    }

    public async Task MarkAllReadAsync(int userId, CancellationToken cancellationToken = default)
    {
        var unread = await _context.UserNotifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var n in unread)
            n.MarkAsRead();
    }
}
