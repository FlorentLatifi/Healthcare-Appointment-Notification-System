using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Events;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Healthcare.Adapters.Events.Handlers;

public sealed class LogPaymentFailedHandler : IDomainEventHandler<PaymentFailedEvent>
{
    private readonly ILogger<LogPaymentFailedHandler> _logger;
    private readonly IAuditLogRepository _auditLogRepo;
    private readonly IUnitOfWork _unitOfWork;

    public LogPaymentFailedHandler(
        ILogger<LogPaymentFailedHandler> logger,
        IAuditLogRepository auditLogRepo,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _auditLogRepo = auditLogRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(
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

        var details = JsonSerializer.Serialize(new
        {
            domainEvent.PaymentId,
            domainEvent.AppointmentId,
            domainEvent.FailureReason
        });

        var entry = new AuditLogEntry(
            "PaymentFailed",
            "Payment",
            domainEvent.PaymentId,
            domainEvent.OccurredOn,
            details,
            null);

        await _auditLogRepo.AddAsync(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
