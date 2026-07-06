using Healthcare.Application.Common;
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

    public async Task<PagedResult<Patient>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var all = await base.GetAllAsync();
        var list = all.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ToList();
        var totalCount = list.Count;
        var items = list.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<Patient>(items, pageNumber, pageSize, totalCount);
    }

    public Task<IEnumerable<Patient>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return FindAsync(p => p.IsActive);
    }

    public async Task<PagedResult<Patient>> GetPagedActiveAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var all = await FindAsync(p => p.IsActive);
        var list = all.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ToList();
        var totalCount = list.Count;
        var items = list.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<Patient>(items, pageNumber, pageSize, totalCount);
    }

    public Task<IEnumerable<Patient>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var lowerSearch = searchTerm.ToLowerInvariant();
        return FindAsync(p =>
            p.FirstName.ToLowerInvariant().Contains(lowerSearch) ||
            p.LastName.ToLowerInvariant().Contains(lowerSearch));
    }

    public async Task<PagedResult<Patient>> GetPagedSearchByNameAsync(
        string searchTerm,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var lowerSearch = searchTerm.ToLowerInvariant();
        var all = await FindAsync(p =>
            p.FirstName.ToLowerInvariant().Contains(lowerSearch) ||
            p.LastName.ToLowerInvariant().Contains(lowerSearch));
        var list = all.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ToList();
        var totalCount = list.Count;
        var items = list.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<Patient>(items, pageNumber, pageSize, totalCount);
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
