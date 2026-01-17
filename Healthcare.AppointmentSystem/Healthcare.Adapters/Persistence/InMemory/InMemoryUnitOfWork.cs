using Healthcare.Application.Ports.Repositories;

namespace Healthcare.Adapters.Persistence.InMemory;

/// <summary>
/// In-memory implementation of Unit of Work pattern.
/// </summary>
/// <remarks>
/// Design Pattern: Unit of Work Pattern + Adapter Pattern
/// 
/// Purpose:
/// - Coordinates multiple repository operations
/// - Ensures all changes succeed or fail together (atomicity)
/// - Manages transaction lifecycle
/// 
/// In-Memory Behavior:
/// - Since we're using in-memory storage, transactions are simulated
/// - All changes are immediately visible (no actual commit/rollback)
/// - In production with EF Core, this would use DbContext.SaveChanges()
/// 
/// Thread Safety:
/// - Uses lock to ensure atomic operations
/// - Safe for concurrent access
/// </remarks>
public sealed class InMemoryUnitOfWork : IUnitOfWork
{
    private readonly object _lock = new();
    private bool _isInTransaction = false;

    public IAppointmentRepository Appointments { get; }
    public IPatientRepository Patients { get; }
    public IDoctorRepository Doctors { get; }

    public IUserRepository Users { get; }
    public IPaymentRepository Payments { get; } 

    /// <summary>
    /// Initializes a new instance with shared repository instances.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: All repositories must share the same storage instances
    /// to ensure data consistency. This is managed by DI container.
    /// </remarks>
    public InMemoryUnitOfWork(
        IAppointmentRepository appointments,
        IPatientRepository patients,
        IDoctorRepository doctors,
        IUserRepository users,
        IPaymentRepository payments)
    {
        Appointments = appointments;
        Patients = patients;
        Doctors = doctors;
        Users = users;
        Payments = payments; 
    }

    /// <summary>
    /// Saves all changes made in this unit of work.
    /// </summary>
    /// <returns>Number of state entries written (always 1 for in-memory).</returns>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            // In-memory implementation: changes are already persisted
            // In real EF Core: await _dbContext.SaveChangesAsync(cancellationToken)
            return Task.FromResult(1);
        }
    }

    /// <summary>
    /// Begins a new transaction.
    /// </summary>
    public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_isInTransaction)
            {
                throw new InvalidOperationException("Transaction already in progress.");
            }

            _isInTransaction = true;
            // In real EF Core: await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_isInTransaction)
            {
                throw new InvalidOperationException("No transaction in progress.");
            }

            _isInTransaction = false;
            // In real EF Core: await _dbContext.Database.CurrentTransaction.CommitAsync(cancellationToken)
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_isInTransaction)
            {
                throw new InvalidOperationException("No transaction in progress.");
            }

            _isInTransaction = false;
            // In real EF Core: await _dbContext.Database.CurrentTransaction.RollbackAsync(cancellationToken)

            // Note: In-memory cannot truly rollback - data is already changed
            // This is a simulation for consistency with the interface
            return Task.CompletedTask;
        }
    }
}