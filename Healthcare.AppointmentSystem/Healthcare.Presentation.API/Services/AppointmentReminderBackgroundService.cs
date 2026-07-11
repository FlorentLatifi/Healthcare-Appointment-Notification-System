using System.Diagnostics;
using Healthcare.Adapters.Background;
using Healthcare.Application.Ports.Notifications;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;

namespace Healthcare.Presentation.API.Services;

/// <summary>
/// Sends appointment reminders on a schedule with Polly retry/circuit breaker,
/// graceful shutdown, metrics, health state, and alerting hooks.
/// </summary>
public sealed class AppointmentReminderBackgroundService : BackgroundService
{
    public const string WorkerName = AppointmentReminderHealthState.Name;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AppointmentReminderBackgroundService> _logger;
    private readonly ReminderSettings _settings;
    private readonly ReminderMetrics _metrics;
    private readonly AppointmentReminderHealthState _health;
    private readonly IBackgroundWorkerAlert _alerts;
    private readonly ResiliencePipeline _batchPipeline;

    public AppointmentReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<AppointmentReminderBackgroundService> logger,
        IOptions<ReminderSettings> settings,
        ReminderMetrics metrics,
        AppointmentReminderHealthState health,
        IBackgroundWorkerAlert alerts)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = settings.Value;
        _metrics = metrics;
        _health = health;
        _alerts = alerts;

        _batchPipeline = BackgroundResiliencePipelineFactory.Create(
            WorkerName,
            _settings.BatchPollyRetryAttempts,
            _settings.BatchPollyRetryBaseDelay,
            _settings.CircuitBreakerFailureThreshold,
            _settings.CircuitBreakDuration,
            health,
            alerts,
            logger);
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _health.MarkEnabled(_settings.Enabled);
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{Worker} graceful shutdown requested (timeout={Timeout}s)",
            WorkerName, _settings.ShutdownTimeout.TotalSeconds);
        _health.MarkStopping();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_settings.ShutdownTimeout);

        try
        {
            await base.StopAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("{Worker} shutdown timed out after {Timeout}s",
                WorkerName, _settings.ShutdownTimeout.TotalSeconds);
            await SafeAlertAsync(new BackgroundWorkerAlert(
                WorkerName,
                BackgroundWorkerAlertSeverity.Warning,
                "shutdown_timeout",
                "Appointment reminder worker did not finish before shutdown timeout."),
                cancellationToken);
        }
        finally
        {
            _health.MarkStopped();
            _logger.LogInformation("{Worker} stopped", WorkerName);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _health.MarkEnabled(false);
            _logger.LogInformation("{Worker} is disabled (ReminderSettings.Enabled=false).", WorkerName);
            return;
        }

        _health.MarkStarted();
        _logger.LogInformation(
            "{Worker} started (interval={IntervalMinutes}m, pollyRetries={PollyRetries}, circuit={Threshold}/{Break}s)",
            WorkerName,
            _settings.IntervalMinutes,
            _settings.BatchPollyRetryAttempts,
            _settings.CircuitBreakerFailureThreshold,
            _settings.CircuitBreakerBreakSeconds);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnceSafeAsync(stoppingToken);

                if (stoppingToken.IsCancellationRequested)
                    break;

                var delay = _health.Snapshot().IsCircuitOpen
                    ? _settings.CircuitBreakDuration
                    : _settings.Interval;

                if (!await DelaySafeAsync(delay, stoppingToken))
                    break;
            }
        }
        finally
        {
            _health.MarkStopped();
            _logger.LogInformation("{Worker} execute loop exited", WorkerName);
        }
    }

    private async Task RunOnceSafeAsync(CancellationToken stoppingToken)
    {
        _health.MarkAttempt();
        var sw = Stopwatch.StartNew();

        try
        {
            var processed = await _batchPipeline.ExecuteAsync(
                static async (state, ct) => await state.ProcessBatchAsync(ct),
                this,
                stoppingToken);

            sw.Stop();
            _metrics.RecordBatch(processed, sw.Elapsed.TotalMilliseconds, success: true);
            _health.MarkSuccess();

            _logger.LogDebug(
                "{Worker} batch ok in {ElapsedMs}ms (appointments={Count})",
                WorkerName, sw.ElapsedMilliseconds, processed);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("{Worker} batch cancelled (host stopping)", WorkerName);
        }
        catch (BrokenCircuitException ex)
        {
            sw.Stop();
            _metrics.RecordBatch(0, sw.Elapsed.TotalMilliseconds, success: false);
            _metrics.RecordCircuitOpen();
            _health.MarkFailure(ex);
            _logger.LogWarning("{Worker} skipped batch — circuit is open", WorkerName);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _metrics.RecordBatch(0, sw.Elapsed.TotalMilliseconds, success: false);
            _health.MarkFailure(ex);

            _logger.LogError(ex, "{Worker} batch failed after Polly retries", WorkerName);

            await SafeAlertAsync(new BackgroundWorkerAlert(
                WorkerName,
                BackgroundWorkerAlertSeverity.Error,
                "batch_failed",
                "Appointment reminder batch failed after retries.",
                ex,
                new Dictionary<string, object?>
                {
                    ["ElapsedMs"] = sw.ElapsedMilliseconds,
                    ["ConsecutiveFailures"] = _health.Snapshot().ConsecutiveFailures
                }), stoppingToken);
        }
    }

    /// <summary>Public for unit tests.</summary>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var scope = _scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var appointments = (await unitOfWork.Appointments
            .GetAppointmentsNeedingRemindersAsync(cancellationToken))
            .ToList();

        _metrics.SetLastBatchSize(appointments.Count);

        _logger.LogInformation(
            "{Worker} found {Count} appointment(s) needing reminders",
            WorkerName, appointments.Count);

        var successCount = 0;

        foreach (var appointment in appointments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ok = await ProcessAppointmentAsync(
                appointment, unitOfWork, notificationService, cancellationToken);
            if (ok)
                successCount++;
        }

        return successCount;
    }

    private async Task<bool> ProcessAppointmentAsync(
        Appointment appointment,
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        try
        {
            var prefs = appointment.Patient?.NotificationPreferences;
            if (prefs is { EmailEnabled: false })
            {
                _logger.LogInformation(
                    "{Worker} email disabled for patient {PatientId}; marking reminder for appointment {AppointmentId}",
                    WorkerName, appointment.PatientId, appointment.Id);

                appointment.MarkReminderSent();
                await unitOfWork.Appointments.UpdateAsync(appointment, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                _metrics.RecordSkipped("email_disabled");
                return true;
            }

            await notificationService.SendAppointmentReminderAsync(appointment, cancellationToken);

            appointment.MarkReminderSent();
            await unitOfWork.Appointments.UpdateAsync(appointment, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            _metrics.RecordSent();
            _logger.LogInformation(
                "{Worker} reminder sent for appointment {AppointmentId}",
                WorkerName, appointment.Id);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _metrics.RecordFailed();
            _logger.LogError(ex,
                "{Worker} failed reminder for appointment {AppointmentId}",
                WorkerName, appointment.Id);

            // Per-item failure should not kill the batch; alert if volume is elevated via metrics.
            return false;
        }
    }

    private static async Task<bool> DelaySafeAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
    }

    private async Task SafeAlertAsync(BackgroundWorkerAlert alert, CancellationToken ct)
    {
        try
        {
            await _alerts.NotifyAsync(alert, ct.IsCancellationRequested ? CancellationToken.None : ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "{Worker} alert sink failed for {Code}", WorkerName, alert.Code);
        }
    }
}
