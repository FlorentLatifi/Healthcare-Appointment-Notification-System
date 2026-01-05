using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;

namespace Healthcare.Application.Ports.Repositories;

/// <summary>
/// Repository interface for Doctor aggregate.
/// </summary>
public interface IDoctorRepository
{
    /// <summary>
    /// Gets a doctor by their unique identifier.
    /// </summary>
    Task<Doctor?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a doctor by their email address.
    /// </summary>
    Task<Doctor?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all doctors in the system.
    /// </summary>
    Task<IEnumerable<Doctor>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active doctors.
    /// </summary>
    Task<IEnumerable<Doctor>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all doctors accepting new patients.
    /// </summary>
    Task<IEnumerable<Doctor>> GetAcceptingPatientsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all doctors with a specific specialty.
    /// </summary>
    Task<IEnumerable<Doctor>> GetBySpecialtyAsync(Specialty specialty, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches doctors by name (first or last name contains search term).
    /// </summary>
    Task<IEnumerable<Doctor>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a doctor with the given email exists.
    /// </summary>
    Task<bool> ExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new doctor to the repository.
    /// </summary>
    Task AddAsync(Doctor doctor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing doctor.
    /// </summary>
    Task UpdateAsync(Doctor doctor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a doctor by their ID.
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}