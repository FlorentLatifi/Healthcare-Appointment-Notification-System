namespace Healthcare.Application.Ports.Repositories;

/// <summary>
/// Unit of Work interface for managing transactions across multiple repositories.
/// </summary>
/// <remarks>
/// Design Pattern: Unit of Work Pattern
/// 
/// The Unit of Work maintains a list of objects affected by a business transaction
/// and coordinates the writing out of changes and resolution of concurrency problems.
/// 
/// Benefits:
/// - Ensures all changes succeed or fail together (atomicity)
/// - Reduces database round trips
/// - Manages transaction lifecycle
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    /// Gets the appointment repository.
    /// </summary>
    IAppointmentRepository Appointments { get; }

    /// <summary>
    /// Gets the patient repository.
    /// </summary>
    IPatientRepository Patients { get; }

    /// <summary>
    /// Gets the doctor repository.
    /// </summary>
    IDoctorRepository Doctors { get; }

    /// <summary>
    /// Saves all changes made in this unit of work to the underlying data store.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of state entries written to the data store.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a new transaction.
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}