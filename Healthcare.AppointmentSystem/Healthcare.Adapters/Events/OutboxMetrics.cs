using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Healthcare.Adapters.Events;

/// <summary>
/// OpenTelemetry-compatible metrics for outbox relay.
/// Meter name: <c>Healthcare.Outbox</c>
/// </summary>
public sealed class OutboxMetrics : IDisposable
{
    public const string MeterName = "Healthcare.Outbox";

    private readonly Meter _meter;
    private readonly Counter<long> _processed;
    private readonly Counter<long> _failed;
    private readonly Counter<long> _deadLettered;
    private readonly Counter<long> _retries;
    private readonly Counter<long> _batches;
    private readonly Counter<long> _circuitOpens;
    private readonly Histogram<double> _processingDurationMs;
    private readonly Histogram<double> _batchDurationMs;

    private long _pendingEstimate;

    public OutboxMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");

        _processed = _meter.CreateCounter<long>(
            "outbox.messages.processed",
            unit: "{message}",
            description: "Outbox messages successfully dispatched");

        _failed = _meter.CreateCounter<long>(
            "outbox.messages.failed",
            unit: "{message}",
            description: "Transient outbox dispatch failures (will retry)");

        _deadLettered = _meter.CreateCounter<long>(
            "outbox.messages.deadlettered",
            unit: "{message}",
            description: "Messages moved to dead-letter");

        _retries = _meter.CreateCounter<long>(
            "outbox.messages.retries",
            unit: "{message}",
            description: "Retry attempts scheduled");

        _batches = _meter.CreateCounter<long>(
            "outbox.batches",
            unit: "{batch}",
            description: "Relay batches executed");

        _circuitOpens = _meter.CreateCounter<long>(
            "outbox.circuit_opens",
            unit: "{event}",
            description: "Times the outbox circuit breaker opened");

        _processingDurationMs = _meter.CreateHistogram<double>(
            "outbox.message.duration",
            unit: "ms",
            description: "Per-message processing duration");

        _batchDurationMs = _meter.CreateHistogram<double>(
            "outbox.batch.duration",
            unit: "ms",
            description: "Batch processing duration");

        _meter.CreateObservableGauge(
            "outbox.messages.pending_estimate",
            () => Volatile.Read(ref _pendingEstimate),
            unit: "{message}",
            description: "Last observed pending-due message count");
    }

    public void SetPendingEstimate(long count) =>
        Volatile.Write(ref _pendingEstimate, count);

    public void RecordProcessed(string eventType, double durationMs)
    {
        _processed.Add(1, new KeyValuePair<string, object?>("event_type", eventType));
        _processingDurationMs.Record(durationMs, new KeyValuePair<string, object?>("event_type", eventType));
    }

    public void RecordFailed(string eventType, double durationMs)
    {
        _failed.Add(1, new KeyValuePair<string, object?>("event_type", eventType));
        _retries.Add(1, new KeyValuePair<string, object?>("event_type", eventType));
        _processingDurationMs.Record(durationMs, new KeyValuePair<string, object?>("event_type", eventType));
    }

    public void RecordDeadLettered(string eventType, string reason)
    {
        _deadLettered.Add(
            1,
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("reason", reason));
    }

    public void RecordBatch(int messageCount, double durationMs, bool success)
    {
        _batches.Add(
            1,
            new KeyValuePair<string, object?>("success", success),
            new KeyValuePair<string, object?>("count", messageCount));
        _batchDurationMs.Record(durationMs);
    }

    public void RecordCircuitOpen() => _circuitOpens.Add(1);

    public void Dispose() => _meter.Dispose();
}
