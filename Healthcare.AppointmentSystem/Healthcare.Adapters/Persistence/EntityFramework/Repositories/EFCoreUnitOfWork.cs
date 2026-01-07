using Healthcare.Application.Ports.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace Healthcare.Adapters.Persistence.EntityFramework.Repositories;

/// <summary>
/// Entity Framework Core implementation of Unit of Work pattern.
/// </summary>
/// <remarks>
/// Design Pattern: Unit of Work Pattern
/// 
/// EF Core DbContext already implements Unit of Work pattern:
/// - Tracks changes to entities
/// - Batches multiple operations
/// - Executes them in a single transaction via SaveChanges()
/// 
/// This class:
/// - Provides access to all repositories
/// - Coordinates SaveChanges across repositories
/// - Manages explicit transactions when needed
/// - Ensures atomicity (all or nothing)
/// 
/// Transaction Strategy:
/// - Default: SaveChanges creates implicit transaction
/// - Explicit: BeginTransaction for multi-step operations
/// - Rollback: Automatic on exception, manual via RollbackTransaction
/// </remarks>
public sealed class EFCoreUnitOfWork : IUnitOfWork
{
    private readonly HealthcareDbContext _context;
    private IDbContextTransaction? _currentTransaction;

    public EFCoreUnitOfWork(
        HealthcareDbContext context,
        IAppointmentRepository appointments,
        IPatientRepository patients,
        IDoctorRepository doctors)
    {
        _context = context;
        Appointments = appointments;
        Patients = patients;
        Doctors = doctors;
    }

    public IAppointmentRepository Appointments { get; }
    public IPatientRepository Patients { get; }
    public IDoctorRepository Doctors { get; }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // SaveChanges automatically wraps in transaction
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            throw new InvalidOperationException(
                "A transaction is already in progress. Nested transactions are not supported.");
        }

        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("No transaction in progress.");
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            _currentTransaction?.Dispose();
            _currentTransaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("No transaction in progress.");
        }

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            _currentTransaction?.Dispose();
            _currentTransaction = null;
        }
    }
}
