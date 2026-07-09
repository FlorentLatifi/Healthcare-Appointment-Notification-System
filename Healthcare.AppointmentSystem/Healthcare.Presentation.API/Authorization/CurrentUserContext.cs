using System.Security.Claims;

namespace Healthcare.Presentation.API.Authorization;

public static class CurrentUserContext
{
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    public static int? GetPatientId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst("patient_id");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }

    public static int? GetDoctorId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst("doctor_id");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }

    public static string GetRole(this ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.Role)!.Value;
}
