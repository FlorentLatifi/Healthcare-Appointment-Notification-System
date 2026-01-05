using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Repositories;

namespace Healthcare.Application.Queries.GetAppointmentsByPatient;

/// <summary>
/// Handler for GetAppointmentsByPatientQuery.
/// </summary>
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
        try
        {
            // 1. Verify patient exists
            var patient = await _patientRepository.GetByIdAsync(query.PatientId, cancellationToken);
            if (patient is null)
            {
                return Result<IEnumerable<AppointmentDto>>.Failure(
                    $"Patient with ID {query.PatientId} not found.");
            }

            // 2. Fetch appointments
            var appointments = await _appointmentRepository
                .GetByPatientIdAsync(query.PatientId, cancellationToken);

            // 3. Map to DTOs
            var dtos = appointments.Select(appointment => new AppointmentDto
            {
                Id = appointment.Id,
                Patient = new PatientDto
                {
                    Id = appointment.Patient.Id,
                    FirstName = appointment.Patient.FirstName,
                    LastName = appointment.Patient.LastName,
                    FullName = appointment.Patient.FullName,
                    Email = appointment.Patient.Email.Value,
                    PhoneNumber = appointment.Patient.PhoneNumber.Value,
                    DateOfBirth = appointment.Patient.DateOfBirth,
                    Age = appointment.Patient.Age,
                    Gender = appointment.Patient.Gender.ToString(),
                    Address = appointment.Patient.Address.GetFullAddress(),
                    IsActive = appointment.Patient.IsActive,
                    CreatedAt = appointment.Patient.CreatedAt
                },
                Doctor = new DoctorDto
                {
                    Id = appointment.Doctor.Id,
                    FirstName = appointment.Doctor.FirstName,
                    LastName = appointment.Doctor.LastName,
                    FullName = appointment.Doctor.FullName,
                    Email = appointment.Doctor.Email.Value,
                    PhoneNumber = appointment.Doctor.PhoneNumber.Value,
                    LicenseNumber = appointment.Doctor.LicenseNumber,
                    Specialties = appointment.Doctor.Specialties.Select(s => s.ToString()).ToList(),
                    ConsultationFeeAmount = appointment.Doctor.ConsultationFee.Amount,
                    ConsultationFeeCurrency = appointment.Doctor.ConsultationFee.Currency,
                    IsAcceptingPatients = appointment.Doctor.IsAcceptingPatients,
                    IsActive = appointment.Doctor.IsActive,
                    YearsOfExperience = appointment.Doctor.YearsOfExperience,
                    CreatedAt = appointment.Doctor.CreatedAt
                },
                ScheduledTime = appointment.ScheduledTime.Value,
                ScheduledDate = appointment.ScheduledTime.GetDate().ToString("yyyy-MM-dd"),
                ScheduledTimeFormatted = appointment.ScheduledTime.ToDisplayString(),
                Status = appointment.Status.ToString(),
                Reason = appointment.Reason,
                DoctorNotes = appointment.DoctorNotes,
                CancellationReason = appointment.CancellationReason,
                ConsultationFeeAmount = appointment.ConsultationFee.Amount,
                ConsultationFeeCurrency = appointment.ConsultationFee.Currency,
                ConfirmedAt = appointment.ConfirmedAt,
                CompletedAt = appointment.CompletedAt,
                CancelledAt = appointment.CancelledAt,
                CreatedAt = appointment.CreatedAt
            })
            .OrderByDescending(a => a.ScheduledTime) // Most recent first
            .ToList();

            return Result<IEnumerable<AppointmentDto>>.Success(dtos);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<AppointmentDto>>.Failure(
                $"An unexpected error occurred: {ex.Message}");
        }
    }
}