namespace Healthcare.Adapters.Background;

/// <summary>
/// Process-local health snapshot for a background worker (shared with ASP.NET health checks).
/// </summary>
public class WorkerHealthState
{
    private readonly object _gate = new();

    public WorkerHealthState(string workerName)
    {
        WorkerName = workerName ?? throw new ArgumentNullException(nameof(workerName));
    }

    public string WorkerName { get; }

    public bool IsEnabled { get; private set; } = true;
    public bool IsRunning { get; private set; }
    public bool IsStopping { get; private set; }
    public bool IsCircuitOpen { get; private set; }

    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? LastAttemptUtc { get; private set; }
    public DateTime? LastSuccessUtc { get; private set; }
    public DateTime? LastFailureUtc { get; private set; }
    public string? LastError { get; private set; }
    public long SuccessfulBatches { get; private set; }
    public long FailedBatches { get; private set; }
    public int ConsecutiveFailures { get; private set; }

    public void MarkEnabled(bool enabled)
    {
        lock (_gate) IsEnabled = enabled;
    }

    public void MarkStarted()
    {
        lock (_gate)
        {
            IsRunning = true;
            IsStopping = false;
            StartedAtUtc = DateTime.UtcNow;
        }
    }

    public void MarkStopping()
    {
        lock (_gate) IsStopping = true;
    }

    public void MarkStopped()
    {
        lock (_gate)
        {
            IsRunning = false;
            IsStopping = false;
        }
    }

    public void MarkAttempt()
    {
        lock (_gate) LastAttemptUtc = DateTime.UtcNow;
    }

    public void MarkSuccess()
    {
        lock (_gate)
        {
            LastSuccessUtc = DateTime.UtcNow;
            LastError = null;
            ConsecutiveFailures = 0;
            SuccessfulBatches++;
        }
    }

    public void MarkFailure(Exception? exception)
    {
        lock (_gate)
        {
            LastFailureUtc = DateTime.UtcNow;
            LastError = exception is null
                ? "Unknown failure"
                : $"{exception.GetType().Name}: {exception.Message}";
            if (LastError.Length > 500)
                LastError = LastError[..500];
            ConsecutiveFailures++;
            FailedBatches++;
        }
    }

    public void MarkCircuitOpen()
    {
        lock (_gate) IsCircuitOpen = true;
    }

    public void MarkCircuitClosed()
    {
        lock (_gate) IsCircuitOpen = false;
    }

    public WorkerHealthSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new WorkerHealthSnapshot(
                WorkerName,
                IsEnabled,
                IsRunning,
                IsStopping,
                IsCircuitOpen,
                StartedAtUtc,
                LastAttemptUtc,
                LastSuccessUtc,
                LastFailureUtc,
                LastError,
                SuccessfulBatches,
                FailedBatches,
                ConsecutiveFailures);
        }
    }
}

public sealed record WorkerHealthSnapshot(
    string WorkerName,
    bool IsEnabled,
    bool IsRunning,
    bool IsStopping,
    bool IsCircuitOpen,
    DateTime? StartedAtUtc,
    DateTime? LastAttemptUtc,
    DateTime? LastSuccessUtc,
    DateTime? LastFailureUtc,
    string? LastError,
    long SuccessfulBatches,
    long FailedBatches,
    int ConsecutiveFailures);
