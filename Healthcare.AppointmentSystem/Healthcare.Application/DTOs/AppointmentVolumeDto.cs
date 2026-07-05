namespace Healthcare.Application.DTOs;

public sealed class AppointmentVolumeDto
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public string GroupBy { get; set; } = string.Empty;
    public List<AppointmentVolumeItemDto> Items { get; set; } = new();
}

public sealed class AppointmentVolumeItemDto
{
    public string Period { get; set; } = string.Empty;
    public int Created { get; set; }
    public int Confirmed { get; set; }
    public int Cancelled { get; set; }
}
