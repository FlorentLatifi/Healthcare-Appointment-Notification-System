using Healthcare.Application.Common;
using Healthcare.Application.DTOs;

namespace Healthcare.Application.Commands.UpdateDoctorSchedule;

/// <summary>Replace a doctor's weekly working hours (all 7 days).</summary>
public sealed class UpdateDoctorScheduleCommand : ICommand<Result>
{
    public int DoctorId { get; set; }

    /// <summary>Full week; each day should appear once.</summary>
    public List<WorkingHoursDto> WeeklySchedule { get; set; } = new();
}
