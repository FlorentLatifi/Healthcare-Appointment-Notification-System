using System.Diagnostics.Metrics;

namespace Healthcare.Presentation.API.Services;

/// <summary>
/// OpenTelemetry metrics for appointment reminder worker. Meter: <c>Healthcare.Reminders</c>
/// </summary>
public sealed class ReminderMetrics : IDisposable
{
    public const string MeterName = "Healthcare.Reminders";

    private readonly Meter _meter;
    private readonly Counter<long> _sent;
    private readonly Counter<long> _skipped;
    private readonly Counter<long> _failed;
    private readonly Counter<long> _batches;
    private readonly Counter<long> _circuitOpens;
    private readonly Histogram<double> _batchDurationMs;

    private long _lastBatchSize;

    public ReminderMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");

        _sent = _meter.CreateCounter<long>(
            "reminders.sent",
            unit: "{reminder}",
            description: "Appointment reminders successfully sent");

        _skipped = _meter.CreateCounter<long>(
            "reminders.skipped",
            unit: "{reminder}",
            description: "Reminders skipped (e.g. email disabled) but still marked");

        _failed = _meter.CreateCounter<long>(
            "reminders.failed",
            unit: "{reminder}",
            description: "Per-appointment reminder failures");

        _batches = _meter.CreateCounter<long>(
            "reminders.batches",
            unit: "{batch}",
            description: "Reminder batches executed");

        _circuitOpens = _meter.CreateCounter<long>(
            "reminders.circuit_opens",
            unit: "{event}",
            description: "Times the reminder circuit breaker opened");

        _batchDurationMs = _meter.CreateHistogram<double>(
            "reminders.batch.duration",
            unit: "ms",
            description: "Reminder batch duration");

        _meter.CreateObservableGauge(
            "reminders.batch.last_size",
            () => Volatile.Read(ref _lastBatchSize),
            unit: "{appointment}",
            description: "Size of last reminder batch");
    }

    public void SetLastBatchSize(int count) =>
        Volatile.Write(ref _lastBatchSize, count);

    public void RecordSent() => _sent.Add(1);

    public void RecordSkipped(string reason) =>
        _skipped.Add(1, new KeyValuePair<string, object?>("reason", reason));

    public void RecordFailed() => _failed.Add(1);

    public void RecordBatch(int size, double durationMs, bool success)
    {
        SetLastBatchSize(size);
        _batches.Add(1, new KeyValuePair<string, object?>("success", success));
        _batchDurationMs.Record(durationMs);
    }

    public void RecordCircuitOpen() => _circuitOpens.Add(1);

    public void Dispose() => _meter.Dispose();
}
