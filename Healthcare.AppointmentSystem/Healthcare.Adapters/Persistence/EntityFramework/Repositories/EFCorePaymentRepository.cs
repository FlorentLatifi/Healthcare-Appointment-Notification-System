using Healthcare.Application.Common;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Queries.Analytics;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.Adapters.Persistence.EntityFramework.Repositories;

public sealed class EFCorePaymentRepository : IPaymentRepository
{
    private readonly HealthcareDbContext _context;

    public EFCorePaymentRepository(HealthcareDbContext context)
    {
        _context = context;
    }

    public async Task<Payment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        // Commands mutate Payment; Appointment join only when needed by callers.
        return await _context.Payments
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Payment?> GetByAppointmentIdAsync(int appointmentId, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .FirstOrDefaultAsync(p => p.AppointmentId == appointmentId, cancellationToken);
    }

    public async Task<Payment?> GetByTransactionIdAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .FirstOrDefaultAsync(p => p.TransactionId != null && p.TransactionId.Value == transactionId, cancellationToken);
    }

    public async Task<IEnumerable<Payment>> GetByStatusAsync(PaymentStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .AsNoTracking()
            .Where(p => p.Status == status)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Payment>> GetByPatientIdAsync(int patientId, CancellationToken cancellationToken = default)
    {
        // Join only when filtering by patient (AppointmentId is unique on Payments)
        return await _context.Payments
            .AsNoTracking()
            .Where(p => p.Appointment != null && p.Appointment.PatientId == patientId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Payment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Payment>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await _context.Payments.CountAsync(cancellationToken);

        var items = await _context.Payments
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Payment>(items, pageNumber, pageSize, totalCount);
    }

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        await _context.Payments.AddAsync(payment, cancellationToken);
    }

    public Task UpdateAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        _context.Payments.Update(payment);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var payment = await _context.Payments.FindAsync(new object[] { id }, cancellationToken);
        if (payment != null)
        {
            _context.Payments.Remove(payment);
        }
    }

    public async Task<decimal> GetTotalRevenueAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Where(p => p.Status == PaymentStatus.Succeeded &&
                        p.PaidAt >= from &&
                        p.PaidAt < to)
            .SumAsync(p => p.Amount.Amount, cancellationToken);
    }

    public async Task<List<DoctorRevenueResult>> GetRevenueByDoctorAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Join(_context.Appointments, p => p.AppointmentId, a => a.Id, (p, a) => new { p, a })
            .Join(_context.Doctors, x => x.a.DoctorId, d => d.Id, (x, d) => new { x.p, d })
            .Where(x => x.p.Status == PaymentStatus.Succeeded &&
                        x.p.PaidAt >= from &&
                        x.p.PaidAt < to)
            .GroupBy(x => new { x.d.Id, x.d.FirstName, x.d.LastName })
            .Select(g => new DoctorRevenueResult(
                g.Key.Id,
                g.Key.FirstName,
                g.Key.LastName,
                g.Sum(x => x.p.Amount.Amount)))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<SpecialtyRevenueResult>> GetRevenueBySpecialtyAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Join(_context.Appointments, p => p.AppointmentId, a => a.Id, (p, a) => new { p, a })
            .Where(x => x.p.Status == PaymentStatus.Succeeded &&
                        x.p.PaidAt >= from &&
                        x.p.PaidAt < to)
            .Join(_context.Set<DoctorSpecialty>(), x => x.a.DoctorId, ds => EF.Property<int>(ds, "DoctorId"), (x, ds) => new { x.p, ds })
            .GroupBy(x => x.ds.Specialty)
            .Select(g => new SpecialtyRevenueResult(
                Enum.GetName(typeof(Specialty), g.Key) ?? "Unknown",
                g.Sum(x => x.p.Amount.Amount)))
            .ToListAsync(cancellationToken);
    }
}
