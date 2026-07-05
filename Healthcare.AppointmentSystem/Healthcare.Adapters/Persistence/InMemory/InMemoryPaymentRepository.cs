using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Queries.Analytics;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;

namespace Healthcare.Adapters.Persistence.InMemory;

/// <summary>
/// In-memory implementation of IPaymentRepository.
/// </summary>
public sealed class InMemoryPaymentRepository : InMemoryRepository<Payment>, IPaymentRepository
{
    public Task<Payment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return base.GetByIdAsync(id);
    }

    public async Task<Payment?> GetByAppointmentIdAsync(int appointmentId, CancellationToken cancellationToken = default)
    {
        var payments = await FindAsync(p => p.AppointmentId == appointmentId);
        return payments.FirstOrDefault();
    }

    public async Task<Payment?> GetByTransactionIdAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        var payments = await FindAsync(p =>
            p.TransactionId != null && p.TransactionId.Value == transactionId);
        return payments.FirstOrDefault();
    }

    public Task<IEnumerable<Payment>> GetByStatusAsync(PaymentStatus status, CancellationToken cancellationToken = default)
    {
        return FindAsync(p => p.Status == status);
    }

    public async Task<IEnumerable<Payment>> GetByPatientIdAsync(int patientId, CancellationToken cancellationToken = default)
    {
        // This requires joining with appointments - in-memory limitation
        // For now, return empty (implement in EF Core version)
        return await Task.FromResult(Enumerable.Empty<Payment>());
    }

    public Task<IEnumerable<Payment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return base.GetAllAsync();
    }

    public Task AddAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        return base.AddAsync(payment);
    }

    public Task UpdateAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        return base.UpdateAsync(payment);
    }

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        return base.DeleteAsync(id);
    }

    public async Task<decimal> GetTotalRevenueAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var payments = await FindAsync(p =>
            p.Status == PaymentStatus.Succeeded &&
            p.PaidAt >= from &&
            p.PaidAt < to);
        return payments.Sum(p => p.Amount.Amount);
    }

    public Task<List<DoctorRevenueResult>> GetRevenueByDoctorAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("InMemoryPaymentRepository does not support GetRevenueByDoctorAsync. Use EFCorePaymentRepository in production.");
    }

    public Task<List<SpecialtyRevenueResult>> GetRevenueBySpecialtyAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("InMemoryPaymentRepository does not support GetRevenueBySpecialtyAsync. Use EFCorePaymentRepository in production.");
    }
}