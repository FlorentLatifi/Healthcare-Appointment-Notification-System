using System.Text.Json;
using Healthcare.Application.Common;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Queries.Analytics;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
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
        return await _context.Payments
            .Include(p => p.Appointment)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Payment?> GetByAppointmentIdAsync(int appointmentId, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Include(p => p.Appointment)
            .FirstOrDefaultAsync(p => p.AppointmentId == appointmentId, cancellationToken);
    }

    public async Task<Payment?> GetByTransactionIdAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Include(p => p.Appointment)
            .FirstOrDefaultAsync(p => p.TransactionId != null && p.TransactionId.Value == transactionId, cancellationToken);
    }

    public async Task<IEnumerable<Payment>> GetByStatusAsync(PaymentStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Include(p => p.Appointment)
            .Where(p => p.Status == status)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Payment>> GetByPatientIdAsync(int patientId, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Include(p => p.Appointment)
            .Where(p => p.Appointment!.PatientId == patientId)
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Payment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Include(p => p.Appointment)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Payment>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Payments
            .Include(p => p.Appointment)
            .AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
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
        var doctorRevenue = await _context.Payments
            .Join(_context.Appointments, p => p.AppointmentId, a => a.Id, (p, a) => new { p, a })
            .Where(x => x.p.Status == PaymentStatus.Succeeded &&
                        x.p.PaidAt >= from &&
                        x.p.PaidAt < to)
            .GroupBy(x => x.a.DoctorId)
            .Select(g => new
            {
                DoctorId = g.Key,
                Revenue = g.Sum(x => x.p.Amount.Amount)
            })
            .ToListAsync(cancellationToken);

        var doctorIds = doctorRevenue.Select(d => d.DoctorId).Distinct().ToList();
        var doctors = await _context.Doctors
            .Where(d => doctorIds.Contains(d.Id))
            .Select(d => new { d.Id, SpecialtiesJson = EF.Property<string>(d, "_specialtiesJson") })
            .ToListAsync(cancellationToken);

        var specialtyLookup = doctors
            .SelectMany(d =>
            {
                var specialties = DeserializeSpecialties(d.SpecialtiesJson);
                var revenue = doctorRevenue.First(r => r.DoctorId == d.Id).Revenue;
                return specialties.Select(s => new SpecialtyRevenueResult(s, revenue));
            })
            .GroupBy(r => r.Specialty)
            .Select(g => new SpecialtyRevenueResult(g.Key, g.Sum(r => r.Revenue)))
            .ToList();

        return specialtyLookup;
    }

    private static List<string> DeserializeSpecialties(string json)
    {
        try
        {
            var values = JsonSerializer.Deserialize<List<int>>(json);
            if (values is null || values.Count == 0)
                return new List<string> { "Unknown" };

            return values
                .Select(v => Enum.IsDefined(typeof(Specialty), v)
                    ? Enum.GetName(typeof(Specialty), v) ?? "Unknown"
                    : "Unknown")
                .ToList();
        }
        catch
        {
            return new List<string> { "Unknown" };
        }
    }
}
