using Healthcare.Domain.Entities;

namespace Healthcare.Application.Ports.Repositories;

/// <summary>
/// Repository interface for Patient aggregate.
/// </summary>
public interface IPatientRepository
{
    /// <summary>
    /// Gets a patient by their unique identifier.
    /// </summary>
    Task<Patient?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a patient by their email address.
    /// </summary>
    Task<Patient?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all patients in the system.
    /// </summary>
    Task<IEnumerable<Patient>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active patients.
    /// </summary>
    Task<IEnumerable<Patient>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches patients by name (first or last name contains search term).
    /// </summary>
    Task<IEnumerable<Patient>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a patient with the given email exists.
    /// </summary>
    Task<bool> ExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new patient to the repository.
    /// </summary>
    Task AddAsync(Patient patient, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing patient.
    /// </summary>
    Task UpdateAsync(Patient patient, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a patient by their ID.
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}