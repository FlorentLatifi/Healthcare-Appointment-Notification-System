using Healthcare.Domain.Entities;

namespace Healthcare.Application.Ports.Repositories;

/// <summary>
/// Repository interface for User aggregate.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string username, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Users linked to the given patient profile (usually zero or one).</summary>
    Task<IReadOnlyList<User>> FindByPatientIdAsync(int patientId, CancellationToken cancellationToken = default);

    /// <summary>Users linked to the given doctor profile (usually zero or one).</summary>
    Task<IReadOnlyList<User>> FindByDoctorIdAsync(int doctorId, CancellationToken cancellationToken = default);
}