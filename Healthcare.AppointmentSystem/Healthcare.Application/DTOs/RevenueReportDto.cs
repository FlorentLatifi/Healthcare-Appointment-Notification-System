namespace Healthcare.Application.DTOs;

public sealed class RevenueReportDto
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public decimal TotalRevenue { get; set; }
    public string Currency { get; set; } = string.Empty;
    public List<RevenueByDoctorItemDto>? ByDoctor { get; set; }
    public List<RevenueBySpecialtyItemDto>? BySpecialty { get; set; }
}

public sealed class RevenueByDoctorItemDto
{
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}

public sealed class RevenueBySpecialtyItemDto
{
    public string Specialty { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}
