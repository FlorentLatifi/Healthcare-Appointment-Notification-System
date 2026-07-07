using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.Adapters.Persistence.EntityFramework.Repositories;

public sealed class EFCoreUserSessionRepository : IUserSessionRepository
{
    private readonly HealthcareDbContext _context;

    public EFCoreUserSessionRepository(HealthcareDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        await _context.UserSessions.AddAsync(session, cancellationToken);
    }

    public Task<UserSession?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return _context.UserSessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public Task<List<UserSession>> GetActiveByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return _context.UserSessions
            .Where(s => s.UserId == userId && !s.IsRevoked)
            .OrderByDescending(s => s.LastUsedAt)
            .ToListAsync(cancellationToken);
    }

    public Task UpdateAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        _context.UserSessions.Update(session);
        return Task.CompletedTask;
    }
}
