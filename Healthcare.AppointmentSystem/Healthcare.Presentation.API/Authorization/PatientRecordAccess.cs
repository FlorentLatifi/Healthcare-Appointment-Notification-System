using System.Security.Claims;
using Healthcare.Application.Ports.Repositories;

namespace Healthcare.Presentation.API.Authorization;

/// <summary>
/// Centralized PHI access rules for patient-scoped appointment/profile reads.
/// Policy (Option A): Doctors may access a patient only when they share a care relationship
/// (at least one appointment together). Admins retain unrestricted access.
/// </summary>
public static class PatientRecordAccess
{
    /// <summary>
    /// Returns null when access is allowed; otherwise a short deny reason for logging/audit.
    /// </summary>
    public static async Task<string?> GetDenyReasonForPatientDataAsync(
        ClaimsPrincipal user,
        int patientId,
        IAppointmentRepository appointments,
        CancellationToken cancellationToken = default)
    {
        var role = user.GetRole();

        if (role == AppRoles.Admin)
            return null;

        if (role == AppRoles.Patient)
        {
            if (user.GetPatientId() != patientId)
                return "patient_not_owner";
            return null;
        }

        if (role == AppRoles.Doctor)
        {
            var doctorId = user.GetDoctorId();
            if (doctorId is null)
                return "doctor_profile_not_linked";

            var hasRelationship = await appointments.HasDoctorPatientCareRelationshipAsync(
                doctorId.Value, patientId, cancellationToken);

            if (!hasRelationship)
                return "doctor_no_care_relationship";

            return null;
        }

        return "role_not_permitted";
    }

    /// <summary>
    /// Whether the caller is a Doctor who must receive only doctor-scoped rows for this patient.
    /// </summary>
    public static bool MustScopeAppointmentsToDoctor(ClaimsPrincipal user)
        => user.GetRole() == AppRoles.Doctor;
}
