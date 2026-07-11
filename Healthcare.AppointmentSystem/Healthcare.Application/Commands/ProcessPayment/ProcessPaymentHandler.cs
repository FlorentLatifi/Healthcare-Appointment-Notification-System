using Healthcare.Application.Common;
using Healthcare.Application.Observability;
using Healthcare.Application.Ports.Payments;
using Microsoft.Extensions.Logging;

namespace Healthcare.Application.Commands.ProcessPayment;

public sealed class ProcessPaymentHandler : ICommandHandler<ProcessPaymentCommand, Result<int>>
{
    private readonly IPaymentGateway _paymentGateway;
    private readonly IPaymentReconciliationService _reconciliationService;
    private readonly IBusinessMetrics _metrics;
    private readonly ILogger<ProcessPaymentHandler> _logger;

    public ProcessPaymentHandler(
        IPaymentGateway paymentGateway,
        IPaymentReconciliationService reconciliationService,
        IBusinessMetrics metrics,
        ILogger<ProcessPaymentHandler> logger)
    {
        _paymentGateway = paymentGateway;
        _reconciliationService = reconciliationService;
        _metrics = metrics;
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

        return result;
    }
}
