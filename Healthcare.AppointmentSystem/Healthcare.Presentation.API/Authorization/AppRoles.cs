using Healthcare.Domain.Enums;

namespace Healthcare.Presentation.API.Authorization;

/// <summary>
/// Role names used by API authorization policies and attributes.
/// </summary>
public static class AppRoles
{
    public const string Patient = nameof(UserRole.Patient);
    public const string Doctor = nameof(UserRole.Doctor);
    public const string Admin = nameof(UserRole.Admin);

    public const string DoctorOrAdmin = Doctor + "," + Admin;
    public const string AdminOrDoctor = Admin + "," + Doctor;
    public const string PatientOrDoctorOrAdmin = Patient + "," + Doctor + "," + Admin;
}
