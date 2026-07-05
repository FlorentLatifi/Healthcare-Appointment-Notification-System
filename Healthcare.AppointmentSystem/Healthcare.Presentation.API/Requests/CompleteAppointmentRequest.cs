namespace Healthcare.Presentation.API.Requests;

public sealed class CompleteAppointmentRequest
{
    public int AppointmentId { get; set; }
    public string DoctorNotes { get; set; } = string.Empty;
}
