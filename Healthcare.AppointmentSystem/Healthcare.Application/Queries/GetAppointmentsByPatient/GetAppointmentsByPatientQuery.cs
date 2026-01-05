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

    public GetAppointmentsByPatientQuery(int patientId)
    {
        PatientId = patientId;
    }
}