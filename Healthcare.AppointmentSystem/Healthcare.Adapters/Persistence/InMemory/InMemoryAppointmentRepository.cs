using Healthcare.Application.Common;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Queries.Analytics;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;

namespace Healthcare.Adapters.Persistence.InMemory;

/// <summary>
/// In-memory implementation of IAppointmentRepository.
/// </summary>
/// <remarks>
/// Design Pattern: Adapter Pattern + Repository Pattern
/// 
/// This adapter:
/// - Implements the PORT (IAppointmentRepository)
/// - Uses in-memory storage (List) for simplicity
/// - Is REPLACEABLE with a real database implementation
/// - Contains NO business logic (just data access)
/// 
/// Production Alternative:
/// Replace this with EntityFrameworkAppointmentRepository that uses
/// DbContext and SQL Server/PostgreSQL.
/// </remarks>
public sealed class InMemoryAppointmentRepository : InMemoryRepository<Appointment>, IAppointmentRepository
{
    public Task<Appointment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return base.GetByIdAsync(id);
    }

    public Task<IEnumerable<Appointment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return base.GetAllAsync();
    }

    public async Task<PagedResult<Appointment>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var all = await base.GetAllAsync();
        var list = all.OrderByDescending(a => a.ScheduledTime.Value).ToList();
        var totalCount = list.Count;
        var items = list.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<Appointment>(items, pageNumber, pageSize, totalCount);
    }

    public Task<IEnumerable<Appointment>> GetByPatientIdAsync(int patientId, CancellationToken cancellationToken = default)
    {
        return FindAsync(a => a.PatientId == patientId);
    }

    public async Task<PagedResult<Appointment>> GetPagedByPatientIdAsync(
        int patientId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var all = await FindAsync(a => a.PatientId == patientId);
        var list = all.OrderByDescending(a => a.ScheduledTime.Value).ToList();
        var totalCount = list.Count;
        var items = list.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<Appointment>(items, pageNumber, pageSize, totalCount);
    }

    public Task<IEnumerable<Appointment>> GetByDoctorIdAsync(int doctorId, CancellationToken cancellationToken = default)
    {
        return FindAsync(a => a.DoctorId == doctorId);
    }

    public async Task<PagedResult<Appointment>> GetPagedByDoctorIdAsync(
        int doctorId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var all = await FindAsync(a => a.DoctorId == doctorId);
        var list = all.OrderByDescending(a => a.ScheduledTime.Value).ToList();
        var totalCount = list.Count;
        var items = list.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<Appointment>(items, pageNumber, pageSize, totalCount);
    }

    public Task<IEnumerable<Appointment>> GetByDoctorAndDateAsync(
        int doctorId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        // Get all appointments for doctor on specific date
        return FindAsync(a =>
            a.DoctorId == doctorId &&
            a.ScheduledTime.Value.Date == date.Date);
    }

    public Task<IEnumerable<Appointment>> GetByStatusAsync(
        AppointmentStatus status,
        CancellationToken cancellationToken = default)
    {
        return FindAsync(a => a.Status == status);
    }

    public Task<IEnumerable<Appointment>> GetAppointmentsNeedingRemindersAsync(
        CancellationToken cancellationToken = default)
    {
        // Get confirmed appointments within next 24 hours
        var now = DateTime.UtcNow;
        var twentyFourHoursFromNow = now.AddHours(24);

        return FindAsync(a =>
            a.Status == AppointmentStatus.Confirmed &&
            a.ScheduledTime.Value > now &&
            a.ScheduledTime.Value <= twentyFourHoursFromNow);
    }

    public Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        return base.AddAsync(appointment);
    }

    public Task UpdateAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        return base.UpdateAsync(appointment);
    }

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        return base.DeleteAsync(id);
    }

    public async Task<StatusCountsResult> GetStatusCountsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var appointments = (await FindAsync(a => a.ScheduledTime.Value >= from && a.ScheduledTime.Value < to)).ToList();
        return new StatusCountsResult(
            appointments.Count(a => a.Status == AppointmentStatus.Pending),
            appointments.Count(a => a.Status == AppointmentStatus.Confirmed),
            appointments.Count(a => a.Status == AppointmentStatus.Completed),
            appointments.Count(a => a.Status == AppointmentStatus.Cancelled),
            appointments.Count(a => a.Status == AppointmentStatus.NoShow));
    }

    public async Task<List<DailyVolumeResult>> GetDailyVolumeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var appointments = (await FindAsync(a => a.ScheduledTime.Value >= from && a.ScheduledTime.Value < to)).ToList();
        return appointments
            .GroupBy(a => a.ScheduledTime.Value.Date)
            .Select(g => new DailyVolumeResult(
                g.Key,
                g.Count(a => a.Status == AppointmentStatus.Pending),
                g.Count(a => a.Status == AppointmentStatus.Confirmed),
                g.Count(a => a.Status == AppointmentStatus.Cancelled)))
            .OrderBy(r => r.Date)
            .ToList();
    }

    public async Task<List<WeeklyVolumeResult>> GetWeeklyVolumeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var daily = await GetDailyVolumeAsync(from, to, cancellationToken);
        return daily
            .GroupBy(d => GetIsoWeek(d.Date))
            .Select(g => new WeeklyVolumeResult(g.Key.Year, g.Key.Week, g.Sum(d => d.Created), g.Sum(d => d.Confirmed), g.Sum(d => d.Cancelled)))
            .OrderBy(r => r.Year).ThenBy(r => r.Week)
            .ToList();
    }

    private static (int Year, int Week) GetIsoWeek(DateTime date)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var cal = culture.Calendar;
        var week = cal.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        var year = date.Year;
        if (week >= 52 && date.Month == 1) year--;
        if (week <= 1 && date.Month == 12) year++;
        return (year, week);
    }
}