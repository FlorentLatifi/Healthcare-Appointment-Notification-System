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
    /// Status/RemindedAt filter in SQL; time window client-side because
    /// <c>AppointmentTime.Value</c> is not EF-translatable with the VO converter.
    /// </summary>
    public async Task<IEnumerable<Appointment>> GetAppointmentsNeedingRemindersAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var twentyFourHoursFromNow = now.AddHours(24);

        var candidates = await _context.Appointments
            .AsNoTracking()
            .Include(a => a.Patient)
            .Where(a => a.Status == AppointmentStatus.Confirmed && a.RemindedAt == null)
            .ToListAsync(cancellationToken);

        return candidates
            .Where(a => a.ScheduledTime.Value > now && a.ScheduledTime.Value <= twentyFourHoursFromNow)
            .OrderBy(a => a.ScheduledTime.Value)
            .ToList();
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
        // Date range filter is client-side: AppointmentTime.Value is not EF-translatable with HasConversion.
        var inRange = await LoadAppointmentsInTimeRangeAsync(from, to, cancellationToken);

        return new StatusCountsResult(
            inRange.Count(a => a.Status == AppointmentStatus.Pending),
            inRange.Count(a => a.Status == AppointmentStatus.Confirmed),
            inRange.Count(a => a.Status == AppointmentStatus.Completed),
            inRange.Count(a => a.Status == AppointmentStatus.Cancelled),
            inRange.Count(a => a.Status == AppointmentStatus.NoShow));
    }

    public async Task<List<DailyVolumeResult>> GetDailyVolumeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var inRange = await LoadAppointmentsInTimeRangeAsync(from, to, cancellationToken);

        return inRange
            .GroupBy(a => a.ScheduledTime.Value.Date)
            .Select(g => new DailyVolumeResult(
                g.Key,
                g.Count(a => a.Status == AppointmentStatus.Pending),
                g.Count(a => a.Status == AppointmentStatus.Confirmed),
                g.Count(a => a.Status == AppointmentStatus.Cancelled)))
            .OrderBy(r => r.Date)
            .ToList();
    }

    /// <summary>
    /// Loads appointments then filters by <see cref="Appointment.ScheduledTime"/> in-process.
    /// Avoids non-translatable <c>ScheduledTime.Value</c> comparisons against DateTime parameters.
    /// </summary>
    private async Task<List<Appointment>> LoadAppointmentsInTimeRangeAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var all = await _context.Appointments
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return all
            .Where(a => a.ScheduledTime.Value >= from && a.ScheduledTime.Value < to)
            .ToList();
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
