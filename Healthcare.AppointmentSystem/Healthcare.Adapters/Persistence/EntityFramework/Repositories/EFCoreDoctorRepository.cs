using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.Adapters.Persistence.EntityFramework.Repositories;

public sealed class EFCoreDoctorRepository : IDoctorRepository
{
    private readonly HealthcareDbContext _context;

    public EFCoreDoctorRepository(HealthcareDbContext context)
    {
        _context = context;
    }

    public async Task<Doctor?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Doctors
            .Include(d => d.SpecialtyEntries)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<Doctor?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = Email.Create(email);

        return await _context.Doctors
            .Include(d => d.SpecialtyEntries)
            .FirstOrDefaultAsync(d => d.Email == normalizedEmail, cancellationToken);
    }

    public async Task<IEnumerable<Doctor>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Doctors
            .Include(d => d.SpecialtyEntries)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Doctor>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Doctors
            .Include(d => d.SpecialtyEntries)
            .Where(d => d.IsActive)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Doctor>> GetAcceptingPatientsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Doctors
            .Include(d => d.SpecialtyEntries)
            .Where(d => d.IsActive && d.IsAcceptingPatients)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Doctor>> GetBySpecialtyAsync(
        Specialty specialty,
        CancellationToken cancellationToken = default)
    {
        return await _context.Doctors
            .Include(d => d.SpecialtyEntries)
            .Where(d => d.SpecialtyEntries.Any(e => e.Specialty == specialty))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Doctor>> SearchByNameAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        var lowerSearch = searchTerm.ToLower();

        return await _context.Doctors
            .Include(d => d.SpecialtyEntries)
            .Where(d => d.FirstName.ToLower().Contains(lowerSearch) ||
                       d.LastName.ToLower().Contains(lowerSearch))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = Email.Create(email);

        return await _context.Doctors
            .AnyAsync(d => d.Email == normalizedEmail, cancellationToken);
    }

    public async Task AddAsync(Doctor doctor, CancellationToken cancellationToken = default)
    {
        await _context.Doctors.AddAsync(doctor, cancellationToken);
    }

    public Task UpdateAsync(Doctor doctor, CancellationToken cancellationToken = default)
    {
        _context.Doctors.Update(doctor);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var doctor = await _context.Doctors
            .FindAsync(new object[] { id }, cancellationToken);

        if (doctor != null)
        {
            _context.Doctors.Remove(doctor);
        }
    }
}
