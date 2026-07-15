namespace Healthcare.Presentation.API.Responses;

/// <summary>
/// Response after creating a Patient or Doctor profile that is linked to the current user.
/// Includes a re-issued access token so the SPA can update claims without calling /Auth/refresh.
/// </summary>
/// <remarks>
/// <see cref="Id"/> is the new profile id. Session fields are populated when the create
/// endpoint re-issues a JWT for the authenticated self-service user. Admin catalog creates
/// may leave <see cref="Token"/> null (no claim change for the admin).
/// </remarks>
public sealed class ProfileCreatedResponse
{
    /// <summary>Created patient or doctor profile id.</summary>
    public int Id { get; set; }

    /// <summary>Fresh JWT access token with updated patient_id / doctor_id claims (when issued).</summary>
    public string? Token { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public string? Username { get; set; }

    public string? Role { get; set; }

    public int? PatientId { get; set; }

    public int? DoctorId { get; set; }
}
