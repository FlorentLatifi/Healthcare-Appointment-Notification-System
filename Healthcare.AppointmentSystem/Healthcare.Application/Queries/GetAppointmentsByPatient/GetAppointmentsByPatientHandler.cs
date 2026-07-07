using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Mappings;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Events;

namespace Healthcare.Application.Queries.GetAppointmentsByPatient;

public sealed class GetAppointmentsByPatientHandler
    : IQueryHandler<GetAppointmentsByPatientQuery, Result<IEnumerable<AppointmentDto>>>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public GetAppointmentsByPatientHandler(
        IAppointmentRepository appointmentRepository,
        IPatientRepository patientRepository,
        IDomainEventDispatcher eventDispatcher)
    {
        _appointmentRepository = appointmentRepository;
        _patientRepository = patientRepository;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<Result<IEnumerable<AppointmentDto>>> HandleAsync(
        GetAppointmentsByPatientQuery query,
        CancellationToken cancellationToken = default)
    {
        var patient = await _patientRepository
            .GetByIdAsync(query.PatientId, cancellationToken);

        if (patient is null)
            return Result<IEnumerable<AppointmentDto>>.Failure(
                $"Patient with ID {query.PatientId} not found.");

        // ── Read-Access Audit ──────────────────────────────────────────────
        // Skip audit for self-access (Patient role viewing own appointments).
        // Only non-Patient roles (Doctor, Admin) are logged to avoid noise.
        if (!string.Equals(query.AccessedByRole, "Patient", StringComparison.OrdinalIgnoreCase))
        {
            await _eventDispatcher.DispatchAsync(new PatientRecordAccessedEvent(
                query.PatientId,
                query.AccessedByUserId,
                "Patient appointments retrieved via GetAppointmentsByPatientQuery"), cancellationToken);
        }

        var appointments = await _appointmentRepository
            .GetByPatientIdAsync(query.PatientId, cancellationToken);

        var result = appointments
            .Select(AppointmentMapper.ToDto)
            .ToList();

        return Result<IEnumerable<AppointmentDto>>.Success(result);
    }
}