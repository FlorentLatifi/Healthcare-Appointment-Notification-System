namespace Healthcare.Presentation.API.Requests;

/// <summary>Self-service / admin update of an existing doctor profile.</summary>
public sealed class UpdateDoctorRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public decimal ConsultationFeeAmount { get; set; }
    public string ConsultationFeeCurrency { get; set; } = "USD";
    public int YearsOfExperience { get; set; }
}
