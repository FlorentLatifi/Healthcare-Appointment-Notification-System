using Healthcare.Application.Common;
using Healthcare.Application.Ports.Payments;
using Microsoft.Extensions.Logging;

namespace Healthcare.Application.Commands.ProcessPayment;

public sealed class ProcessPaymentHandler : ICommandHandler<ProcessPaymentCommand, Result<int>>
{
    private readonly IPaymentGateway _paymentGateway;
    private readonly IPaymentReconciliationService _reconciliationService;
    private readonly ILogger<ProcessPaymentHandler> _logger;

    public ProcessPaymentHandler(
        IPaymentGateway paymentGateway,
        IPaymentReconciliationService reconciliationService,
        ILogger<ProcessPaymentHandler> logger)
    {
        _paymentGateway = paymentGateway;
        _reconciliationService = reconciliationService;
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
            return Result<int>.Failure($"Payment confirmation failed: {confirmationResult.Error}");
        }

        var confirmation = confirmationResult.Value;

        return await _reconciliationService.ReconcilePaymentAsync(
            command.AppointmentId,
            command.PaymentIntentId,
            confirmation.Succeeded,
            confirmation.TransactionId,
            confirmation.PaymentMethod,
            confirmation.FailureReason,
            cancellationToken);
    }
}
