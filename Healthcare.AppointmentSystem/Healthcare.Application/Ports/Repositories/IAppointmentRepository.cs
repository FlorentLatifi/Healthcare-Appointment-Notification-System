using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;

namespace Healthcare.Application.Ports.Repositories;

/// <summary>
/// Repository interface for Appointment aggregate.
/// </summary>
/// <remarks>
/// Design Pattern: Repository Pattern
/// 
/// The repository abstracts data access and provides a collection-like
/// interface for accessing domain objects. It hides the details of how
/// data is stored (database, file, memory, etc.).
/// 
/// Hexagonal Architecture: This is a PORT (interface in the application layer).
/// The ADAPTER (implementation) will be in the Infrastructure layer.
/// </remarks>
public interface IAppointmentRepository
{
    /// <summary>
    /// Gets an appointment by its unique identifier.
    /// </summary>
    /// <param name="id">The appointment ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The appointment if found, null otherwise.</returns>
    Task<Appointment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all appointments in the system.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of all appointments.</returns>
    Task<IEnumerable<Appointment>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all appointments for a specific patient.
    /// </summary>
    /// <param name="patientId">The patient ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of appointments for the patient.</returns>
    Task<IEnumerable<Appointment>> GetByPatientIdAsync(int patientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all appointments for a specific doctor.
    /// </summary>
    /// <param name="doctorId">The doctor ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of appointments for the doctor.</returns>
    Task<IEnumerable<Appointment>> GetByDoctorIdAsync(int doctorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all appointments for a doctor on a specific date.
    /// </summary>
    /// <param name="doctorId">The doctor ID.</param>
    /// <param name="date">The date to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of appointments for the doctor on that date.</returns>
    Task<IEnumerable<Appointment>> GetByDoctorAndDateAsync(
        int doctorId,
        DateTime date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all appointments with a specific status.
    /// </summary>
    /// <param name="status">The appointment status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of appointments with that status.</returns>
    Task<IEnumerable<Appointment>> GetByStatusAsync(
        AppointmentStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all appointments that need reminders (confirmed and within next 24 hours).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of appointments needing reminders.</returns>
    Task<IEnumerable<Appointment>> GetAppointmentsNeedingRemindersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new appointment to the repository.
    /// </summary>
    /// <param name="appointment">The appointment to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing appointment.
    /// </summary>
    /// <param name="appointment">The appointment to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(Appointment appointment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an appointment by its ID.
    /// </summary>
    /// <param name="id">The appointment ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}