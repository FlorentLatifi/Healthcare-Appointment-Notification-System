using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;

namespace Healthcare.Application.Ports.Repositories;

/// <summary>
/// Repository interface for Payment aggregate.
/// </summary>
/// <remarks>
/// Design Pattern: Repository Pattern
/// 
/// Abstracts data access for payments, following the same pattern
/// as other repositories in the system.
/// </remarks>
public interface IPaymentRepository
{
    /// <summary>
    /// Gets a payment by its unique identifier.
    /// </summary>
    Task<Payment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a payment by appointment ID.
    /// </summary>
    Task<Payment?> GetByAppointmentIdAsync(int appointmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a payment by transaction ID.
    /// </summary>
    Task<Payment?> GetByTransactionIdAsync(string transactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all payments with a specific status.
    /// </summary>
    Task<IEnumerable<Payment>> GetByStatusAsync(PaymentStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all payments for a specific patient (via appointments).
    /// </summary>
    Task<IEnumerable<Payment>> GetByPatientIdAsync(int patientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all payments.
    /// </summary>
    Task<IEnumerable<Payment>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new payment to the repository.
    /// </summary>
    Task AddAsync(Payment payment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing payment.
    /// </summary>
    Task UpdateAsync(Payment payment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a payment by its ID.
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}