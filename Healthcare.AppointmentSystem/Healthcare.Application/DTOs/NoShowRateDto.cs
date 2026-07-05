namespace Healthcare.Application.DTOs;

public sealed class NoShowRateDto
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public double NoShowRatePercent { get; set; }
    public int ConfirmedCount { get; set; }
    public int CompletedCount { get; set; }
    public int NoShowCount { get; set; }
    public int TotalCount { get; set; }
}
