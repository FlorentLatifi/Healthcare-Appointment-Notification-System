using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Healthcare.Adapters.Events.Handlers;

/// <summary>
/// Logs payment failure to audit trail.
/// </summary>
public sealed class LogPaymentFailedHandler : IDomainEventHandler<PaymentFailedEvent>
{
    private readonly ILogger<LogPaymentFailedHandler> _logger;

    public LogPaymentFailedHandler(ILogger<LogPaymentFailedHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(
        PaymentFailedEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[AUDIT] Payment {PaymentId} failed at {Timestamp} | " +
            "Appointment: {AppointmentId} | Reason: {Reason}",
            domainEvent.PaymentId,
            domainEvent.OccurredOn,
            domainEvent.AppointmentId,
            domainEvent.FailureReason);

        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine("❌ PAYMENT FAILED - AUDIT LOG");
        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine($"Event ID:        {domainEvent.EventId}");
        Console.WriteLine($"Occurred On:     {domainEvent.OccurredOn:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"Payment ID:      {domainEvent.PaymentId}");
        Console.WriteLine($"Appointment ID:  {domainEvent.AppointmentId}");
        Console.WriteLine($"Failure Reason:  {domainEvent.FailureReason}");
        Console.WriteLine("═══════════════════════════════════════════════");

        return Task.CompletedTask;
    }
}