namespace Healthcare.Application.DTOs;

/// <summary>Cached weekly working-hours for a doctor (rarely changes).</summary>
public sealed class DoctorScheduleDto
{
    public int DoctorId { get; set; }
    public bool IsActive { get; set; }
    public bool IsAcceptingPatients { get; set; }
    public List<WorkingHoursDto> WeeklySchedule { get; set; } = new();
}

public sealed class WorkingHoursDto
{
    public DayOfWeek DayOfWeek { get; set; }
    /// <summary>HH:mm or null when day off.</summary>
    public string? StartTime { get; set; }
    /// <summary>HH:mm or null when day off.</summary>
    public string? EndTime { get; set; }
    public bool IsWorkingDay { get; set; }
}
