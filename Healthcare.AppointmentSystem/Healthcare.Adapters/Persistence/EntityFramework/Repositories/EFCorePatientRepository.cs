using Healthcare.Application.Common;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.Adapters.Persistence.EntityFramework.Repositories;

/// <summary>
/// Entity Framework Core implementation of IPatientRepository.
/// </summary>
public sealed class EFCorePatientRepository : IPatientRepository
{
    private readonly HealthcareDbContext _context;

    public EFCorePatientRepository(HealthcareDbContext context)
    {
        _context = context;
    }

    public async Task<Patient?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Patients
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Patient?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = Email.Create(email);

        return await _context.Patients
            .FirstOrDefaultAsync(p => p.Email == normalizedEmail, cancellationToken);
    }

    public async Task<IEnumerable<Patient>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Patients
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Patient>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Patients.AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Patient>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<IEnumerable<Patient>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Patients
            .Where(p => p.IsActive)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Patient>> GetPagedActiveAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Patients
            .Where(p => p.IsActive)
            .AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Patient>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<IEnumerable<Patient>> SearchByNameAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        var lowerSearch = searchTerm.ToLower();

        return await _context.Patients
            .Where(p => p.FirstName.ToLower().Contains(lowerSearch) ||
                       p.LastName.ToLower().Contains(lowerSearch))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Patient>> GetPagedSearchByNameAsync(
        string searchTerm,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var lowerSearch = searchTerm.ToLower();
        var query = _context.Patients
            .Where(p => p.FirstName.ToLower().Contains(lowerSearch) ||
                       p.LastName.ToLower().Contains(lowerSearch))
            .AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Patient>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<bool> ExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = Email.Create(email);

        return await _context.Patients
            .AnyAsync(p => p.Email == normalizedEmail, cancellationToken);
    }

    public async Task AddAsync(Patient patient, CancellationToken cancellationToken = default)
    {
        await _context.Patients.AddAsync(patient, cancellationToken);
    }

    public Task UpdateAsync(Patient patient, CancellationToken cancellationToken = default)
    {
        _context.Patients.Update(patient);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var patient = await _context.Patients
            .FindAsync(new object[] { id }, cancellationToken);

        if (patient != null)
        {
            _context.Patients.Remove(patient);
        }
    }
}
