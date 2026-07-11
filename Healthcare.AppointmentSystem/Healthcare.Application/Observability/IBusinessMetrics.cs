namespace Healthcare.Application.Observability;

/// <summary>
/// Domain / product metrics for dashboards and SLOs (OpenTelemetry Meter: Healthcare.Business).
/// </summary>
public interface IBusinessMetrics
{
    void AppointmentBooked(string? appointmentType = null);
    void AppointmentCancelled(string? reasonCategory = null);
    void AppointmentConfirmed();
    void AppointmentCompleted();
    void AppointmentNoShow();
    void PaymentSucceeded(string? currency = null);
    void PaymentFailed(string? failureCategory = null);
    void PaymentRefunded();
    void LoginSucceeded();
    void LoginFailed(string? reason = null);
    void CommandExecuted(string commandName, bool success, double durationMs);
}
