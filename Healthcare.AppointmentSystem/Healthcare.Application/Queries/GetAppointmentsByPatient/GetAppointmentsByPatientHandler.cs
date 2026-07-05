using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Mappings;
using Healthcare.Application.Ports.Repositories;

namespace Healthcare.Application.Queries.GetAppointmentsByPatient;

public sealed class GetAppointmentsByPatientHandler
    : IQueryHandler<GetAppointmentsByPatientQuery, Result<IEnumerable<AppointmentDto>>>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IPatientRepository _patientRepository;

    public GetAppointmentsByPatientHandler(
        IAppointmentRepository appointmentRepository,
        IPatientRepository patientRepository)
    {
        _appointmentRepository = appointmentRepository;
        _patientRepository = patientRepository;
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

        var appointments = await _appointmentRepository
            .GetByPatientIdAsync(query.PatientId, cancellationToken);

        var result = appointments
            .Select(AppointmentMapper.ToDto)
            .ToList();

        return Result<IEnumerable<AppointmentDto>>.Success(result);
    }
}