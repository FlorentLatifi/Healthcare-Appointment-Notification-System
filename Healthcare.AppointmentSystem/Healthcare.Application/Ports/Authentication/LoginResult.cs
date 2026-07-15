using System;

namespace Healthcare.Application.Ports.Authentication;

/// <summary>
/// Result of a successful login or refresh: tokens plus identity fields for the API response.
/// Identity fields come from the domain user so clients never depend on JWT claim-type maps.
/// </summary>
public sealed class LoginResult
{
    /// <summary>JWT access token.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Opaque refresh token (also set as httpOnly cookie by the API).</summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>Access token expiration (UTC).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Session rotation family id.</summary>
    public Guid FamilyId { get; set; }

    /// <summary>Authenticated user id.</summary>
    public int UserId { get; set; }

    /// <summary>Username for display / client session state.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Role name (Patient, Doctor, Admin).</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Linked patient profile id, if any.</summary>
    public int? PatientId { get; set; }

    /// <summary>Linked doctor profile id, if any.</summary>
    public int? DoctorId { get; set; }
}
