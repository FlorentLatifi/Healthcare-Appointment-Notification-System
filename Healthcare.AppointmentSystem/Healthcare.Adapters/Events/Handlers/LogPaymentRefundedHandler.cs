using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Healthcare.Adapters.Events.Handlers;

/// <summary>
/// Logs payment refund to audit trail.
/// </summary>
public sealed class LogPaymentRefundedHandler : IDomainEventHandler<PaymentRefundedEvent>
{
    private readonly ILogger<LogPaymentRefundedHandler> _logger;

    public LogPaymentRefundedHandler(ILogger<LogPaymentRefundedHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(
        PaymentRefundedEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[AUDIT] Payment {PaymentId} refunded at {Timestamp} | " +
            "Appointment: {AppointmentId} | Amount: {Amount} {Currency} | " +
            "Refund Transaction: {RefundTransactionId}",
            domainEvent.PaymentId,
            domainEvent.OccurredOn,
            domainEvent.AppointmentId,
            domainEvent.Amount.Amount,
            domainEvent.Amount.Currency,
            domainEvent.RefundTransactionId.Value);

        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine("🔄 PAYMENT REFUNDED - AUDIT LOG");
        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine($"Event ID:             {domainEvent.EventId}");
        Console.WriteLine($"Occurred On:          {domainEvent.OccurredOn:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"Payment ID:           {domainEvent.PaymentId}");
        Console.WriteLine($"Appointment ID:       {domainEvent.AppointmentId}");
        Console.WriteLine($"Refund Amount:        {domainEvent.Amount.ToDisplayString()}");
        Console.WriteLine($"Refund Transaction:   {domainEvent.RefundTransactionId.Value}");
        Console.WriteLine("═══════════════════════════════════════════════");

        return Task.CompletedTask;
    }
}