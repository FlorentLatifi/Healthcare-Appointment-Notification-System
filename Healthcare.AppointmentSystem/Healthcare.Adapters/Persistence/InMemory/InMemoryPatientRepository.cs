using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;

namespace Healthcare.Adapters.Persistence.InMemory;

/// <summary>
/// In-memory implementation of IPatientRepository.
/// </summary>
public sealed class InMemoryPatientRepository : InMemoryRepository<Patient>, IPatientRepository
{
    public Task<Patient?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return base.GetByIdAsync(id);
    }

    public Task<Patient?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return FindAsync(p => p.Email.Value.Equals(email, StringComparison.OrdinalIgnoreCase))
            .ContinueWith(t => t.Result.FirstOrDefault(), cancellationToken);
    }

    public Task<IEnumerable<Patient>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return base.GetAllAsync();
    }

    public Task<IEnumerable<Patient>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return FindAsync(p => p.IsActive);
    }

    public Task<IEnumerable<Patient>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var lowerSearch = searchTerm.ToLowerInvariant();
        return FindAsync(p =>
            p.FirstName.ToLowerInvariant().Contains(lowerSearch) ||
            p.LastName.ToLowerInvariant().Contains(lowerSearch));
    }

    public Task<bool> ExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return AnyAsync(p => p.Email.Value.Equals(email, StringComparison.OrdinalIgnoreCase));
    }

    public Task AddAsync(Patient patient, CancellationToken cancellationToken = default)
    {
        return base.AddAsync(patient);
    }

    public Task UpdateAsync(Patient patient, CancellationToken cancellationToken = default)
    {
        return base.UpdateAsync(patient);
    }

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        return base.DeleteAsync(id);
    }
}