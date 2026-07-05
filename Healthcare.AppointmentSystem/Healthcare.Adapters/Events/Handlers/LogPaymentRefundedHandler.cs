using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Events;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Healthcare.Adapters.Events.Handlers;

public sealed class LogPaymentRefundedHandler : IDomainEventHandler<PaymentRefundedEvent>
{
    private readonly ILogger<LogPaymentRefundedHandler> _logger;
    private readonly IAuditLogRepository _auditLogRepo;
    private readonly IUnitOfWork _unitOfWork;

    public LogPaymentRefundedHandler(
        ILogger<LogPaymentRefundedHandler> logger,
        IAuditLogRepository auditLogRepo,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _auditLogRepo = auditLogRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
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

        var details = JsonSerializer.Serialize(new
        {
            domainEvent.PaymentId,
            domainEvent.AppointmentId,
            Amount = domainEvent.Amount.Amount,
            Currency = domainEvent.Amount.Currency,
            RefundTransactionId = domainEvent.RefundTransactionId.Value
        });

        var entry = new AuditLogEntry(
            "PaymentRefunded",
            "Payment",
            domainEvent.PaymentId,
            domainEvent.OccurredOn,
            details,
            null);

        await _auditLogRepo.AddAsync(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
