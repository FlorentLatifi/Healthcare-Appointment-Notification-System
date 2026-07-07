using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Events;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Healthcare.Adapters.Events.Handlers;

public sealed class LogPaymentSucceededHandler : IDomainEventHandler<PaymentSucceededEvent>
{
    private readonly ILogger<LogPaymentSucceededHandler> _logger;
    private readonly IAuditLogRepository _auditLogRepo;
    private readonly IUnitOfWork _unitOfWork;

    public LogPaymentSucceededHandler(
        ILogger<LogPaymentSucceededHandler> logger,
        IAuditLogRepository auditLogRepo,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _auditLogRepo = auditLogRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
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

        var details = JsonSerializer.Serialize(new
        {
            domainEvent.PaymentId,
            domainEvent.AppointmentId,
            Amount = domainEvent.Amount.Amount,
            Currency = domainEvent.Amount.Currency,
            TransactionId = domainEvent.TransactionId.Value
        });

        var entry = new AuditLogEntry(
            "PaymentSucceeded",
            "Payment",
            domainEvent.PaymentId,
            domainEvent.OccurredOn,
            details,
            null);

        await _auditLogRepo.AddAsync(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
