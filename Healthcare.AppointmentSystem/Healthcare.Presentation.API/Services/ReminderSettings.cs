namespace Healthcare.Presentation.API.Services;

public sealed class ReminderSettings
{
    public const string SectionName = "ReminderSettings";

    /// <summary>When false, the worker idles without processing.</summary>
    public bool Enabled { get; set; } = true;

    public int IntervalMinutes { get; set; } = 30;

    /// <summary>Polly retries for a single batch cycle on transient infra errors.</summary>
    public int BatchPollyRetryAttempts { get; set; } = 2;

    public int BatchPollyRetryBaseDelaySeconds { get; set; } = 2;

    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    public int CircuitBreakerBreakSeconds { get; set; } = 60;

    /// <summary>Max time to wait for in-flight batch during host shutdown.</summary>
    public int ShutdownTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Health check becomes Degraded when no successful batch within this many minutes.
    /// 0 disables the staleness check.
    /// </summary>
    public int UnhealthyIfNoSuccessMinutes { get; set; } = 90;

    public TimeSpan Interval => TimeSpan.FromMinutes(Math.Max(1, IntervalMinutes));
    public TimeSpan BatchPollyRetryBaseDelay => TimeSpan.FromSeconds(Math.Max(1, BatchPollyRetryBaseDelaySeconds));
    public TimeSpan CircuitBreakDuration => TimeSpan.FromSeconds(Math.Max(10, CircuitBreakerBreakSeconds));
    public TimeSpan ShutdownTimeout => TimeSpan.FromSeconds(Math.Max(5, ShutdownTimeoutSeconds));
}
