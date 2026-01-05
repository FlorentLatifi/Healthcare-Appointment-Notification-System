using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Repositories;

namespace Healthcare.Application.Queries.GetAppointment;

/// <summary>
/// Handler for GetAppointmentQuery.
/// </summary>
public sealed class GetAppointmentHandler : IQueryHandler<GetAppointmentQuery, Result<AppointmentDto>>
{
    private readonly IAppointmentRepository _appointmentRepository;

    public GetAppointmentHandler(IAppointmentRepository appointmentRepository)
    {
        _appointmentRepository = appointmentRepository;
    }

    public async Task<Result<AppointmentDto>> HandleAsync(
        GetAppointmentQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Fetch appointment
            var appointment = await _appointmentRepository.GetByIdAsync(query.AppointmentId, cancellationToken);

            if (appointment is null)
            {
                return Result<AppointmentDto>.Failure($"Appointment with ID {query.AppointmentId} not found.");
            }

            // 2. Map to DTO
            var dto = new AppointmentDto
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
            };

            return Result<AppointmentDto>.Success(dto);
        }
        catch (Exception ex)
        {
            return Result<AppointmentDto>.Failure($"An unexpected error occurred: {ex.Message}");
        }
    }
}