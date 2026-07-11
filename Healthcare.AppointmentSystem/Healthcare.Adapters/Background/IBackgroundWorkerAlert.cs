namespace Healthcare.Adapters.Background;

/// <summary>
/// Hook for paging / Slack / email when background workers degrade.
/// Default implementation logs; replace in DI for real alerting.
/// </summary>
public interface IBackgroundWorkerAlert
{
    Task NotifyAsync(BackgroundWorkerAlert alert, CancellationToken cancellationToken = default);
}

public enum BackgroundWorkerAlertSeverity
{
    Warning = 1,
    Error = 2,
    Critical = 3
}

public sealed record BackgroundWorkerAlert(
    string WorkerName,
    BackgroundWorkerAlertSeverity Severity,
    string Code,
    string Message,
    Exception? Exception = null,
    IReadOnlyDictionary<string, object?>? Data = null);
