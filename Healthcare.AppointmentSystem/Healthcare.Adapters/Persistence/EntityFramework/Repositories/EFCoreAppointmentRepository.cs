using Healthcare.Application.Common;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Queries.Analytics;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.Adapters.Persistence.EntityFramework.Repositories;

/// <summary>
/// Entity Framework Core implementation of IAppointmentRepository.
/// </summary>
/// <remarks>
/// Design Pattern: Repository Pattern + Adapter Pattern
/// 
/// This adapter:
/// - Implements the PORT (IAppointmentRepository) defined in Application layer
/// - Uses Entity Framework Core for data access
/// - Translates domain queries to SQL via LINQ
/// - Eagerly loads navigation properties to build complete aggregates
/// - Is REPLACEABLE without touching Domain or Application code
/// 
/// Performance Considerations:
/// - Uses .AsNoTracking() for read-only queries (better performance)
/// - Includes related entities (.Include) to avoid N+1 queries
/// - Applies proper indexing via entity configurations
/// </remarks>
public sealed class EFCoreAppointmentRepository : IAppointmentRepository
{
    private readonly HealthcareDbContext _context;

    public EFCoreAppointmentRepository(HealthcareDbContext context)
    {
        _context = context;
    }

    public async Task<Appointment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Appointment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Appointment>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.ScheduledTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Appointment>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<IEnumerable<Appointment>> GetByPatientIdAsync(
        int patientId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.PatientId == patientId)
            .AsNoTracking()
            .OrderByDescending(a => a.ScheduledTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Appointment>> GetPagedByPatientIdAsync(
        int patientId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.PatientId == patientId)
            .AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.ScheduledTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Appointment>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<IEnumerable<Appointment>> GetByDoctorIdAsync(
        int doctorId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.DoctorId == doctorId)
            .AsNoTracking()
            .OrderByDescending(a => a.ScheduledTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Appointment>> GetPagedByDoctorIdAsync(
        int doctorId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.DoctorId == doctorId)
            .AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.ScheduledTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Appointment>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<IEnumerable<Appointment>> GetByDoctorAndDateAsync(
        int doctorId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        return await _context.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.DoctorId == doctorId &&
                       a.ScheduledTime.Value >= startOfDay &&
                       a.ScheduledTime.Value < endOfDay)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Appointment>> GetByStatusAsync(
        AppointmentStatus status,
        CancellationToken cancellationToken = default)
    {
        return await _context.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.Status == status)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Appointment>> GetAppointmentsNeedingRemindersAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var twentyFourHoursFromNow = now.AddHours(24);

        return await _context.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.Status == AppointmentStatus.Confirmed &&
                       a.ScheduledTime.Value > now &&
                       a.ScheduledTime.Value <= twentyFourHoursFromNow &&
                       a.RemindedAt == null)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        await _context.Appointments.AddAsync(appointment, cancellationToken);
    }

    public Task UpdateAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        _context.Appointments.Update(appointment);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var appointment = await _context.Appointments
            .FindAsync(new object[] { id }, cancellationToken);

        if (appointment != null)
        {
            _context.Appointments.Remove(appointment);
        }
    }

    public async Task<StatusCountsResult> GetStatusCountsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var counts = await _context.Appointments
            .Where(a => a.ScheduledTime.Value >= from && a.ScheduledTime.Value < to)
            .GroupBy(a => 1)
            .Select(g => new StatusCountsResult(
                g.Count(a => a.Status == AppointmentStatus.Pending),
                g.Count(a => a.Status == AppointmentStatus.Confirmed),
                g.Count(a => a.Status == AppointmentStatus.Completed),
                g.Count(a => a.Status == AppointmentStatus.Cancelled),
                g.Count(a => a.Status == AppointmentStatus.NoShow)))
            .FirstOrDefaultAsync(cancellationToken);

        return counts ?? new StatusCountsResult(0, 0, 0, 0, 0);
    }

    public async Task<List<DailyVolumeResult>> GetDailyVolumeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        return await _context.Appointments
            .Where(a => a.ScheduledTime.Value >= from && a.ScheduledTime.Value < to)
            .GroupBy(a => new { a.ScheduledTime.Value.Year, a.ScheduledTime.Value.Month, a.ScheduledTime.Value.Day })
            .Select(g => new DailyVolumeResult(
                new DateTime(g.Key.Year, g.Key.Month, g.Key.Day),
                g.Count(a => a.Status == AppointmentStatus.Pending),
                g.Count(a => a.Status == AppointmentStatus.Confirmed),
                g.Count(a => a.Status == AppointmentStatus.Cancelled)))
            .OrderBy(r => r.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WeeklyVolumeResult>> GetWeeklyVolumeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var daily = await GetDailyVolumeAsync(from, to, cancellationToken);

        return daily
            .GroupBy(d => GetIsoWeek(d.Date))
            .Select(g => new WeeklyVolumeResult(
                g.Key.Year,
                g.Key.Week,
                g.Sum(d => d.Created),
                g.Sum(d => d.Confirmed),
                g.Sum(d => d.Cancelled)))
            .OrderBy(r => r.Year)
            .ThenBy(r => r.Week)
            .ToList();
    }

    private static (int Year, int Week) GetIsoWeek(DateTime date)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var cal = culture.Calendar;
        var week = cal.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        var year = date.Year;
        if (week >= 52 && date.Month == 1)
            year--;
        if (week <= 1 && date.Month == 12)
            year++;
        return (year, week);
    }
}