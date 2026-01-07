using Healthcare.Application.Ports.Repositories;
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
                       a.ScheduledTime.Value <= twentyFourHoursFromNow)
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
}