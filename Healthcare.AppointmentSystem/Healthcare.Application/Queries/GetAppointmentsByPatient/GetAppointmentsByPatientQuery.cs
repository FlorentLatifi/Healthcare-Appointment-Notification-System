using Healthcare.Application.Common;
using Healthcare.Application.DTOs;

namespace Healthcare.Application.Queries.GetAppointmentsByPatient;

/// <summary>
/// Query to get all appointments for a specific patient.
/// </summary>
public sealed class GetAppointmentsByPatientQuery : IQuery<Result<IEnumerable<AppointmentDto>>>
{
    /// <summary>
    /// Gets or sets the patient ID.
    /// </summary>
    public int PatientId { get; set; }

    /// <summary>
    /// The UserId of the person accessing this data. Null if unknown.
    /// Used to raise read-access audit events only for non-self access.
    /// </summary>
    public int? AccessedByUserId { get; set; }

    /// <summary>
    /// The role of the person accessing this data. Null if unknown.
    /// Used to skip audit for self-access (Patient role) and log for
    /// Doctor/Admin access.
    /// </summary>
    public string? AccessedByRole { get; set; }

    public GetAppointmentsByPatientQuery(int patientId)
    {
        PatientId = patientId;
    }
}