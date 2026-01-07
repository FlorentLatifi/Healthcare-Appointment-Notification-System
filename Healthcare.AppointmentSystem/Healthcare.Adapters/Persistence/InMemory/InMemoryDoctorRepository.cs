using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;

namespace Healthcare.Adapters.Persistence.InMemory;

/// <summary>
/// In-memory implementation of IDoctorRepository.
/// </summary>
public sealed class InMemoryDoctorRepository : InMemoryRepository<Doctor>, IDoctorRepository
{
    public Task<Doctor?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return base.GetByIdAsync(id);
    }

    public Task<Doctor?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return FindAsync(d => d.Email.Value.Equals(email, StringComparison.OrdinalIgnoreCase))
            .ContinueWith(t => t.Result.FirstOrDefault(), cancellationToken);
    }

    public Task<IEnumerable<Doctor>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return base.GetAllAsync();
    }

    public Task<IEnumerable<Doctor>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return FindAsync(d => d.IsActive);
    }

    public Task<IEnumerable<Doctor>> GetAcceptingPatientsAsync(CancellationToken cancellationToken = default)
    {
        return FindAsync(d => d.IsActive && d.IsAcceptingPatients);
    }

    public Task<IEnumerable<Doctor>> GetBySpecialtyAsync(Specialty specialty, CancellationToken cancellationToken = default)
    {
        return FindAsync(d => d.Specialties.Contains(specialty));
    }

    public Task<IEnumerable<Doctor>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var lowerSearch = searchTerm.ToLowerInvariant();
        return FindAsync(d =>
            d.FirstName.ToLowerInvariant().Contains(lowerSearch) ||
            d.LastName.ToLowerInvariant().Contains(lowerSearch));
    }

    public Task<bool> ExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return AnyAsync(d => d.Email.Value.Equals(email, StringComparison.OrdinalIgnoreCase));
    }

    public Task AddAsync(Doctor doctor, CancellationToken cancellationToken = default)
    {
        return base.AddAsync(doctor);
    }

    public Task UpdateAsync(Doctor doctor, CancellationToken cancellationToken = default)
    {
        return base.UpdateAsync(doctor);
    }

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        return base.DeleteAsync(id);
    }
}