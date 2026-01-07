using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Healthcare.Adapters.Persistence.EntityFramework.Repositories;

/// <summary>
/// Entity Framework Core implementation of IDoctorRepository.
/// </summary>
/// <remarks>
/// Special Handling: Specialties Collection
/// - Specialties are stored as JSON string in database
/// - Converted to List on read, from List on write
/// - This approach keeps the database schema simple while preserving collection
/// </remarks>
public sealed class EFCoreDoctorRepository : IDoctorRepository
{
    private readonly HealthcareDbContext _context;

    public EFCoreDoctorRepository(HealthcareDbContext context)
    {
        _context = context;
    }

    public async Task<Doctor?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var doctor = await _context.Doctors
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (doctor != null)
        {
            LoadSpecialties(doctor);
        }

        return doctor;
    }

    public async Task<Doctor?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var doctor = await _context.Doctors
            .FirstOrDefaultAsync(d => d.Email.Value == email, cancellationToken);

        if (doctor != null)
        {
            LoadSpecialties(doctor);
        }

        return doctor;
    }

    public async Task<IEnumerable<Doctor>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var doctors = await _context.Doctors
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var doctor in doctors)
        {
            LoadSpecialties(doctor);
        }

        return doctors;
    }

    public async Task<IEnumerable<Doctor>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var doctors = await _context.Doctors
            .Where(d => d.IsActive)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var doctor in doctors)
        {
            LoadSpecialties(doctor);
        }

        return doctors;
    }

    public async Task<IEnumerable<Doctor>> GetAcceptingPatientsAsync(
        CancellationToken cancellationToken = default)
    {
        var doctors = await _context.Doctors
            .Where(d => d.IsActive && d.IsAcceptingPatients)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var doctor in doctors)
        {
            LoadSpecialties(doctor);
        }

        return doctors;
    }

    public async Task<IEnumerable<Doctor>> GetBySpecialtyAsync(
        Specialty specialty,
        CancellationToken cancellationToken = default)
    {
        var specialtyValue = (int)specialty;

        // Query JSON column - this works in SQL Server 2016+
        var doctors = await _context.Doctors
            .Where(d => EF.Functions.JsonValue(
                EF.Property<string>(d, "_specialtiesJson"),
                "$") != null)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Filter in memory for specialty (since JSON query is complex)
        var filtered = doctors.Where(d =>
        {
            LoadSpecialties(d);
            return d.Specialties.Contains(specialty);
        }).ToList();

        return filtered;
    }

    public async Task<IEnumerable<Doctor>> SearchByNameAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        var lowerSearch = searchTerm.ToLower();

        var doctors = await _context.Doctors
            .Where(d => d.FirstName.ToLower().Contains(lowerSearch) ||
                       d.LastName.ToLower().Contains(lowerSearch))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var doctor in doctors)
        {
            LoadSpecialties(doctor);
        }

        return doctors;
    }

    public async Task<bool> ExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Doctors
            .AnyAsync(d => d.Email.Value == email, cancellationToken);
    }

    public async Task AddAsync(Doctor doctor, CancellationToken cancellationToken = default)
    {
        SaveSpecialties(doctor);
        await _context.Doctors.AddAsync(doctor, cancellationToken);
    }

    public Task UpdateAsync(Doctor doctor, CancellationToken cancellationToken = default)
    {
        SaveSpecialties(doctor);
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

    /// <summary>
    /// Loads specialties from JSON string into the collection.
    /// </summary>
    private void LoadSpecialties(Doctor doctor)
    {
        var json = _context.Entry(doctor).Property<string>("_specialtiesJson").CurrentValue;

        if (!string.IsNullOrEmpty(json))
        {
            var specialtyInts = JsonSerializer.Deserialize<List<int>>(json) ?? new List<int>();
            var specialties = specialtyInts.Select(i => (Specialty)i).ToList();

            // Use reflection to set private collection
            var field = typeof(Doctor).GetField("_specialties",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                var list = (List<Specialty>)field.GetValue(doctor)!;
                list.Clear();
                list.AddRange(specialties);
            }
        }
    }

    /// <summary>
    /// Saves specialties collection to JSON string.
    /// </summary>
    private void SaveSpecialties(Doctor doctor)
    {
        var specialtyInts = doctor.Specialties.Select(s => (int)s).ToList();
        var json = JsonSerializer.Serialize(specialtyInts);

        _context.Entry(doctor).Property<string>("_specialtiesJson").CurrentValue = json;
    }
}