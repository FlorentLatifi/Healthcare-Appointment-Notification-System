using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;

namespace Healthcare.Adapters.Persistence.InMemory;

/// <summary>
/// In-memory implementation of IUserRepository.
/// </summary>
public sealed class InMemoryUserRepository : InMemoryRepository<User>, IUserRepository
{
    public Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return base.GetByIdAsync(id);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var users = await FindAsync(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        return users.FirstOrDefault();
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var users = await FindAsync(u => u.Email.Value.Equals(email, StringComparison.OrdinalIgnoreCase));
        return users.FirstOrDefault();
    }

    public Task<bool> ExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        return AnyAsync(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
    }

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        return base.AddAsync(user);
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        return base.UpdateAsync(user);
    }

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        return base.DeleteAsync(id);
    }
}