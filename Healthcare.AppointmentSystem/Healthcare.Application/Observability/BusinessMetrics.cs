using System.Diagnostics.Metrics;

namespace Healthcare.Application.Observability;

/// <summary>
/// OpenTelemetry-compatible business counters/histograms.
/// Meter name: <c>Healthcare.Business</c>
/// </summary>
public sealed class BusinessMetrics : IBusinessMetrics, IDisposable
{
    public const string MeterName = "Healthcare.Business";

    private readonly Meter _meter;
    private readonly Counter<long> _appointmentsBooked;
    private readonly Counter<long> _appointmentsCancelled;
    private readonly Counter<long> _appointmentsConfirmed;
    private readonly Counter<long> _appointmentsCompleted;
    private readonly Counter<long> _appointmentsNoShow;
    private readonly Counter<long> _paymentsSucceeded;
    private readonly Counter<long> _paymentsFailed;
    private readonly Counter<long> _paymentsRefunded;
    private readonly Counter<long> _loginsSucceeded;
    private readonly Counter<long> _loginsFailed;
    private readonly Counter<long> _commands;
    private readonly Histogram<double> _commandDurationMs;

    public BusinessMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");

        _appointmentsBooked = _meter.CreateCounter<long>(
            "healthcare.appointments.booked",
            unit: "{appointment}",
            description: "Appointments successfully booked");

        _appointmentsCancelled = _meter.CreateCounter<long>(
            "healthcare.appointments.cancelled",
            unit: "{appointment}",
            description: "Appointments cancelled");

        _appointmentsConfirmed = _meter.CreateCounter<long>(
            "healthcare.appointments.confirmed",
            unit: "{appointment}",
            description: "Appointments confirmed");

        _appointmentsCompleted = _meter.CreateCounter<long>(
            "healthcare.appointments.completed",
            unit: "{appointment}",
            description: "Appointments completed");

        _appointmentsNoShow = _meter.CreateCounter<long>(
            "healthcare.appointments.no_show",
            unit: "{appointment}",
            description: "Appointments marked no-show");

        _paymentsSucceeded = _meter.CreateCounter<long>(
            "healthcare.payments.succeeded",
            unit: "{payment}",
            description: "Successful payments");

        _paymentsFailed = _meter.CreateCounter<long>(
            "healthcare.payments.failed",
            unit: "{payment}",
            description: "Failed payments");

        _paymentsRefunded = _meter.CreateCounter<long>(
            "healthcare.payments.refunded",
            unit: "{payment}",
            description: "Refunded payments");

        _loginsSucceeded = _meter.CreateCounter<long>(
            "healthcare.auth.login.succeeded",
            unit: "{attempt}",
            description: "Successful logins");

        _loginsFailed = _meter.CreateCounter<long>(
            "healthcare.auth.login.failed",
            unit: "{attempt}",
            description: "Failed logins");

        _commands = _meter.CreateCounter<long>(
            "healthcare.commands",
            unit: "{command}",
            description: "Application commands/queries executed");

        _commandDurationMs = _meter.CreateHistogram<double>(
            "healthcare.commands.duration",
            unit: "ms",
            description: "Command/query execution duration");
    }

    public void AppointmentBooked(string? appointmentType = null) =>
        _appointmentsBooked.Add(1, Tag("type", appointmentType ?? "standard"));

    public void AppointmentCancelled(string? reasonCategory = null) =>
        _appointmentsCancelled.Add(1, Tag("reason", reasonCategory ?? "unspecified"));

    public void AppointmentConfirmed() => _appointmentsConfirmed.Add(1);

    public void AppointmentCompleted() => _appointmentsCompleted.Add(1);

    public void AppointmentNoShow() => _appointmentsNoShow.Add(1);

    public void PaymentSucceeded(string? currency = null) =>
        _paymentsSucceeded.Add(1, Tag("currency", currency ?? "unknown"));

    public void PaymentFailed(string? failureCategory = null) =>
        _paymentsFailed.Add(1, Tag("category", failureCategory ?? "unspecified"));

    public void PaymentRefunded() => _paymentsRefunded.Add(1);

    public void LoginSucceeded() => _loginsSucceeded.Add(1);

    public void LoginFailed(string? reason = null) =>
        _loginsFailed.Add(1, Tag("reason", reason ?? "unspecified"));

    public void CommandExecuted(string commandName, bool success, double durationMs)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("command", commandName),
            new("success", success)
        };
        _commands.Add(1, tags);
        _commandDurationMs.Record(durationMs, tags);
    }

    private static KeyValuePair<string, object?> Tag(string key, string value) =>
        new(key, value);

    public void Dispose() => _meter.Dispose();
}
