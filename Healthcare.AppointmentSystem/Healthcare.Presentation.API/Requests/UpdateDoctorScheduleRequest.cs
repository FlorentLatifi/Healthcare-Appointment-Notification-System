using Healthcare.Application.DTOs;

namespace Healthcare.Presentation.API.Requests;

/// <summary>Replace the doctor's full weekly working-hours schedule.</summary>
public sealed class UpdateDoctorScheduleRequest
{
    public List<WorkingHoursDto> WeeklySchedule { get; set; } = new();
}
