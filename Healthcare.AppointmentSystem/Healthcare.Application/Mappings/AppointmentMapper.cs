using Healthcare.Application.DTOs;
using Healthcare.Domain.Entities;

namespace Healthcare.Application.Mappings;

public static class AppointmentMapper
{
    public static AppointmentDto ToDto(Appointment appointment)
    {
        if (appointment.Patient == null || appointment.Doctor == null)
            throw new InvalidOperationException(
                "Appointment must have Patient and Doctor loaded.");

        return new AppointmentDto
        {
            Id = appointment.Id,
            ReferenceCode = appointment.ReferenceCode,
            PatientId = appointment.PatientId,
            DoctorId = appointment.DoctorId,
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
                Specialties = appointment.Doctor.Specialties
                    .Select(s => s.ToString()).ToList(),
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
    }
}
