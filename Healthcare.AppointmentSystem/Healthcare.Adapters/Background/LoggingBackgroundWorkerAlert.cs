using Microsoft.Extensions.Logging;

namespace Healthcare.Adapters.Background;

/// <summary>
/// Default alert sink: structured logs (wire PagerDuty/Teams by replacing this registration).
/// </summary>
public sealed class LoggingBackgroundWorkerAlert : IBackgroundWorkerAlert
{
    private readonly ILogger<LoggingBackgroundWorkerAlert> _logger;

    public LoggingBackgroundWorkerAlert(ILogger<LoggingBackgroundWorkerAlert> logger)
    {
        _logger = logger;
    }

    public Task NotifyAsync(BackgroundWorkerAlert alert, CancellationToken cancellationToken = default)
    {
        // Structured properties for log aggregators / alert rules (e.g. Serilog → Grafana).
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["WorkerName"] = alert.WorkerName,
            ["AlertCode"] = alert.Code,
            ["AlertSeverity"] = alert.Severity.ToString(),
        });

        switch (alert.Severity)
        {
            case BackgroundWorkerAlertSeverity.Critical:
                _logger.LogCritical(
                    alert.Exception,
                    "BACKGROUND_ALERT [{Code}] {Worker}: {Message} {@Data}",
                    alert.Code, alert.WorkerName, alert.Message, alert.Data);
                break;
            case BackgroundWorkerAlertSeverity.Error:
                _logger.LogError(
                    alert.Exception,
                    "BACKGROUND_ALERT [{Code}] {Worker}: {Message} {@Data}",
                    alert.Code, alert.WorkerName, alert.Message, alert.Data);
                break;
            default:
                _logger.LogWarning(
                    alert.Exception,
                    "BACKGROUND_ALERT [{Code}] {Worker}: {Message} {@Data}",
                    alert.Code, alert.WorkerName, alert.Message, alert.Data);
                break;
        }

        return Task.CompletedTask;
    }
}
