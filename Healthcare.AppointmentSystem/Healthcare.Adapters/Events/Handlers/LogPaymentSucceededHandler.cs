using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Healthcare.Adapters.Events.Handlers;

/// <summary>
/// Logs payment success to audit trail.
/// </summary>
/// <remarks>
/// Design Pattern: Observer Pattern
/// 
/// This handler creates an audit log when payment succeeds.
/// In production, this would:
/// - Write to audit database table
/// - Send to external logging service (Elasticsearch, Splunk)
/// - Trigger analytics tracking
/// - Send success notification to finance team
/// </remarks>
public sealed class LogPaymentSucceededHandler : IDomainEventHandler<PaymentSucceededEvent>
{
    private readonly ILogger<LogPaymentSucceededHandler> _logger;

    public LogPaymentSucceededHandler(ILogger<LogPaymentSucceededHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(
        PaymentSucceededEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[AUDIT] Payment {PaymentId} succeeded at {Timestamp} | " +
            "Appointment: {AppointmentId} | Amount: {Amount} {Currency} | " +
            "Transaction: {TransactionId}",
            domainEvent.PaymentId,
            domainEvent.OccurredOn,
            domainEvent.AppointmentId,
            domainEvent.Amount.Amount,
            domainEvent.Amount.Currency,
            domainEvent.TransactionId.Value);

        // Console output for development
        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine("💰 PAYMENT SUCCEEDED - AUDIT LOG");
        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine($"Event ID:        {domainEvent.EventId}");
        Console.WriteLine($"Occurred On:     {domainEvent.OccurredOn:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"Payment ID:      {domainEvent.PaymentId}");
        Console.WriteLine($"Appointment ID:  {domainEvent.AppointmentId}");
        Console.WriteLine($"Amount:          {domainEvent.Amount.ToDisplayString()}");
        Console.WriteLine($"Transaction ID:  {domainEvent.TransactionId.Value}");
        Console.WriteLine("═══════════════════════════════════════════════");

        return Task.CompletedTask;
    }
}