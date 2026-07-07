using Healthcare.Domain.Entities;

namespace Healthcare.Application.Ports.Repositories;

public interface IUserSessionRepository
{
    Task AddAsync(UserSession session, CancellationToken cancellationToken = default);
    Task<UserSession?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<UserSession>> GetActiveByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task UpdateAsync(UserSession session, CancellationToken cancellationToken = default);
}
