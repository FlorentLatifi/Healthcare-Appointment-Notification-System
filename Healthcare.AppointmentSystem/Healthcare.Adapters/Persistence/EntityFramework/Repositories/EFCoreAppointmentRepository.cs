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
/// Performance notes:
/// - Explicit <c>Include</c>/<c>ThenInclude</c> for UI aggregates (no lazy loading).
/// - <c>AsNoTracking</c> on pure reads.
/// - Availability / analytics paths avoid loading Patient/Doctor graphs.
/// - Counts use projection without Includes.
/// </remarks>
public sealed class EFCoreAppointmentRepository : IAppointmentRepository
{
    private readonly HealthcareDbContext _context;

    public EFCoreAppointmentRepository(HealthcareDbContext context)
    {
        _context = context;
    }

    /// <summary>Tracked aggregate with navigations — for command handlers that mutate.</summary>
    public async Task<Appointment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await AppointmentAggregate()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Appointment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await AppointmentAggregateReadOnly()
            .OrderByDescending(a => a.ScheduledTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Appointment>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Count without joins — Include would force unnecessary JOIN work for COUNT(*)
        var totalCount = await _context.Appointments.CountAsync(cancellationToken);

        var items = await AppointmentAggregateReadOnly()
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
        return await AppointmentAggregateReadOnly()
            .Where(a => a.PatientId == patientId)
            .OrderByDescending(a => a.ScheduledTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Appointment>> GetPagedByPatientIdAsync(
        int patientId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = _context.Appointments.Where(a => a.PatientId == patientId);
        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await AppointmentAggregateReadOnly()
            .Where(a => a.PatientId == patientId)
            .OrderByDescending(a => a.ScheduledTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Appointment>(items, pageNumber, pageSize, totalCount);
    }

    public Task<bool> HasDoctorPatientCareRelationshipAsync(
        int doctorId,
        int patientId,
        CancellationToken cancellationToken = default)
    {
        return _context.Appointments.AsNoTracking()
            .AnyAsync(a => a.DoctorId == doctorId && a.PatientId == patientId, cancellationToken);
    }

    public async Task<PagedResult<Appointment>> GetPagedByPatientAndDoctorIdAsync(
        int patientId,
        int doctorId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = _context.Appointments
            .Where(a => a.PatientId == patientId && a.DoctorId == doctorId);
        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await AppointmentAggregateReadOnly()
            .Where(a => a.PatientId == patientId && a.DoctorId == doctorId)
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
        return await AppointmentAggregateReadOnly()
            .Where(a => a.DoctorId == doctorId)
            .OrderByDescending(a => a.ScheduledTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Appointment>> GetPagedByDoctorIdAsync(
        int doctorId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = _context.Appointments.Where(a => a.DoctorId == doctorId);
        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await AppointmentAggregateReadOnly()
            .Where(a => a.DoctorId == doctorId)
            .OrderByDescending(a => a.ScheduledTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Appointment>(items, pageNumber, pageSize, totalCount);
    }

    /// <summary>
    /// Used for double-booking / availability. Does NOT load Patient/Doctor graphs —
    /// only appointment rows for the doctor's day (uses IX_Appointments_Doctor_Time).
    /// </summary>
    public async Task<IEnumerable<Appointment>> GetByDoctorAndDateAsync(
        int doctorId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        // AppointmentTime is a VO with HasConversion; comparing `.Value` is not EF-translatable.
        // Load by doctor (uses IX_Appointments_Doctor_Time), then filter the day range in-process.
        // For a single doctor-day this is small; avoids InvalidCast when binding DateTime params to the VO converter.
        var forDoctor = await _context.Appointments
            .AsNoTracking()
            .Where(a => a.DoctorId == doctorId)
            .ToListAsync(cancellationToken);

        return forDoctor
            .Where(a => a.ScheduledTime.Value >= startOfDay && a.ScheduledTime.Value < endOfDay)
            .OrderBy(a => a.ScheduledTime.Value)
            .ToList();
    }

    public async Task<IEnumerable<Appointment>> GetByStatusAsync(
        AppointmentStatus status,
        CancellationToken cancellationToken = default)
    {
        return await AppointmentAggregateReadOnly()
            .Where(a => a.Status == status)
            .OrderByDescending(a => a.ScheduledTime)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Reminder job: only needs Patient (email prefs) + appointment fields.
    /// Uses filtered index IX_Appointments_Reminders when Status/RemindedAt predicates match.
    /// </summary>
    public async Task<IEnumerable<Appointment>> GetAppointmentsNeedingRemindersAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var twentyFourHoursFromNow = now.AddHours(24);

        return await _context.Appointments
            .AsNoTracking()
            .Include(a => a.Patient)
            .Where(a => a.Status == AppointmentStatus.Confirmed &&
                       a.ScheduledTime.Value > now &&
                       a.ScheduledTime.Value <= twentyFourHoursFromNow &&
                       a.RemindedAt == null)
            .OrderBy(a => a.ScheduledTime)
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
        // Single scan, no navigations — uses IX_Appointments_Status_Time / ScheduledTime range
        var counts = await _context.Appointments
            .AsNoTracking()
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
            .AsNoTracking()
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

    private IQueryable<Appointment> AppointmentAggregate() =>
        _context.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .ThenInclude(d => d.SpecialtyEntries);

    private IQueryable<Appointment> AppointmentAggregateReadOnly() =>
        AppointmentAggregate().AsNoTracking();

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
