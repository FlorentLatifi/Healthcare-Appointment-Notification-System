namespace Healthcare.Domain.Audit;

/// <summary>
/// Canonical action names for immutable security / PHI audit records.
/// Prefer these constants over free-form strings at call sites.
/// </summary>
public static class AuditActions
{
    public const string GetPatientById = "GetPatientById";
    public const string BookAppointment = "BookAppointment";
    public const string ProcessPayment = "ProcessPayment";
    public const string CreatePaymentIntent = "CreatePaymentIntent";
    public const string CreatePatient = "CreatePatient";
    public const string UpdatePatient = "UpdatePatient";
    public const string AnonymizePatient = "AnonymizePatient";
    public const string PatientRecordAccessed = "PatientRecordAccessed";
    public const string GetAppointment = "GetAppointment";
    public const string LoginSucceeded = "LoginSucceeded";
    public const string LoginFailed = "LoginFailed";
    public const string PromoteToAdmin = "PromoteToAdmin";
}
