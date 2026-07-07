using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;

namespace Healthcare.Adapters.Persistence.InMemory;

public sealed class InMemoryUserSessionRepository : IUserSessionRepository
{
    private readonly List<UserSession> _sessions = new();
    private readonly object _lock = new();
    private int _nextId = 1;

    public Task AddAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var idField = typeof(UserSession).GetProperty("Id",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            idField?.SetValue(session, _nextId++);
            _sessions.Add(session);
        }
        return Task.CompletedTask;
    }

    public Task<UserSession?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_sessions.FirstOrDefault(s => s.Id == id));
        }
    }

    public Task<List<UserSession>> GetActiveByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var active = _sessions
                .Where(s => s.UserId == userId && !s.IsRevoked)
                .OrderByDescending(s => s.LastUsedAt)
                .ToList();
            return Task.FromResult(active);
        }
    }

    public Task UpdateAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
