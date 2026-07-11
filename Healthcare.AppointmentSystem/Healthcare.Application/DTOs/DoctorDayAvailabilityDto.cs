namespace Healthcare.Application.DTOs;

/// <summary>
/// Cached booked slots for a doctor on a calendar day (UTC date key).
/// Used for availability UIs and read models — not authoritative for writes.
/// </summary>
public sealed class DoctorDayAvailabilityDto
{
    public int DoctorId { get; set; }
    public DateOnly Date { get; set; }
    public List<BookedSlotDto> BookedSlots { get; set; } = new();
    public DateTime CachedAtUtc { get; set; }
}

public sealed class BookedSlotDto
{
    public int AppointmentId { get; set; }
    public DateTime StartUtc { get; set; }
    public string Status { get; set; } = string.Empty;
}
