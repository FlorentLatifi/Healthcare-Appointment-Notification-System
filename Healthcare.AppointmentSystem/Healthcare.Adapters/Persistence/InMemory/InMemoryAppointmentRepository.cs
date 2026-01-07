using Healthcare.Application.Ports.Repositories;
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

    public Task<IEnumerable<Appointment>> GetByPatientIdAsync(int patientId, CancellationToken cancellationToken = default)
    {
        return FindAsync(a => a.PatientId == patientId);
    }

    public Task<IEnumerable<Appointment>> GetByDoctorIdAsync(int doctorId, CancellationToken cancellationToken = default)
    {
        return FindAsync(a => a.DoctorId == doctorId);
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
}