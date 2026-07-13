using Healthcare.Application.Common;
using Healthcare.Application.Observability;
using Healthcare.Application.Ports.Audit;
using Healthcare.Application.Ports.Payments;
using Healthcare.Domain.Audit;
using Healthcare.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Healthcare.Application.Commands.ProcessPayment;

public sealed class ProcessPaymentHandler : ICommandHandler<ProcessPaymentCommand, Result<int>>
{
    private readonly IPaymentGateway _paymentGateway;
    private readonly IPaymentReconciliationService _reconciliationService;
    private readonly IBusinessMetrics _metrics;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<ProcessPaymentHandler> _logger;

    public ProcessPaymentHandler(
        IPaymentGateway paymentGateway,
        IPaymentReconciliationService reconciliationService,
        IBusinessMetrics metrics,
        IAuditLogService auditLogService,
        ILogger<ProcessPaymentHandler> logger)
    {
        _paymentGateway = paymentGateway;
        _reconciliationService = reconciliationService;
        _metrics = metrics;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<Result<int>> HandleAsync(
        ProcessPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var confirmationResult = await _paymentGateway.ConfirmPaymentAsync(
            command.PaymentIntentId,
            cancellationToken);

        if (confirmationResult.IsFailure)
        {
            _metrics.PaymentFailed("confirmation_failed");
            _logger.LogWarning(
                "Payment confirmation failed Intent={PaymentIntentId} CorrelationId={CorrelationId}",
                command.PaymentIntentId,
                CorrelationContext.Current);

            await _auditLogService.WriteAsync(
                AuditActions.ProcessPayment,
                "Payment",
                resourceId: null,
                AuditOutcome.Failure,
                details: new
                {
                    command.AppointmentId,
                    // Intent id only — not card data
                    PaymentIntentIdPrefix = Prefix(command.PaymentIntentId),
                    Error = "confirmation_failed"
                },
                cancellationToken: cancellationToken);

            return Result<int>.Failure($"Payment confirmation failed: {confirmationResult.Error}");
        }

        var confirmation = confirmationResult.Value;

        var result = await _reconciliationService.ReconcilePaymentAsync(
            command.AppointmentId,
            command.PaymentIntentId,
            confirmation.Succeeded,
            confirmation.TransactionId,
            confirmation.PaymentMethod,
            confirmation.FailureReason,
            cancellationToken);

        if (result.IsSuccess && confirmation.Succeeded)
            _metrics.PaymentSucceeded();
        else if (!confirmation.Succeeded)
            _metrics.PaymentFailed(confirmation.FailureReason ?? "gateway_declined");
        else
            _metrics.PaymentFailed("reconciliation_failed");

        var outcome = result.IsSuccess && confirmation.Succeeded
            ? AuditOutcome.Success
            : AuditOutcome.Failure;

        await _auditLogService.WriteAsync(
            AuditActions.ProcessPayment,
            "Payment",
            resourceId: result.IsSuccess ? result.Value : null,
            outcome,
            details: new
            {
                command.AppointmentId,
                PaymentIntentIdPrefix = Prefix(command.PaymentIntentId),
                confirmation.Succeeded,
                PaymentMethod = confirmation.PaymentMethod
            },
            cancellationToken: cancellationToken);

        return result;
    }

    private static string Prefix(string intentId)
        => string.IsNullOrEmpty(intentId)
            ? string.Empty
            : intentId.Length <= 12 ? intentId : intentId[..12] + "…";
}
