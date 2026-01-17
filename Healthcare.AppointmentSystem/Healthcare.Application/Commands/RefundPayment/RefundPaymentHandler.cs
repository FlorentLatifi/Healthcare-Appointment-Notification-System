using Healthcare.Application.Common;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Payments;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.ValueObjects;

namespace Healthcare.Application.Commands.RefundPayment;

/// <summary>
/// Handler for RefundPaymentCommand.
/// </summary>
public sealed class RefundPaymentHandler : ICommandHandler<RefundPaymentCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public RefundPaymentHandler(
        IUnitOfWork unitOfWork,
        IPaymentGateway paymentGateway,
        IDomainEventDispatcher eventDispatcher)
    {
        _unitOfWork = unitOfWork;
        _paymentGateway = paymentGateway;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<Result> HandleAsync(
        RefundPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Fetch payment
            var payment = await _unitOfWork.Payments
                .GetByIdAsync(command.PaymentId, cancellationToken);

            if (payment == null)
            {
                return Result.Failure($"Payment with ID {command.PaymentId} not found.");
            }

            // 2. Validate payment can be refunded
            if (!payment.CanBeRefunded())
            {
                return Result.Failure($"Payment cannot be refunded. Current status: {payment.Status}");
            }

            // 3. Mark payment as refund pending
            payment.InitiateRefund();

            // 4. Process refund with gateway
            var refundResult = await _paymentGateway.RefundPaymentAsync(
                payment.TransactionId!.Value,
                reason: command.Reason,
                cancellationToken: cancellationToken);

            if (refundResult.IsFailure)
            {
                return Result.Failure($"Refund failed: {refundResult.Error}");
            }

            // 5. Complete refund
            var refundTransactionId = TransactionId.Create(refundResult.Value.RefundId);
            payment.CompleteRefund(refundTransactionId);

            // 6. Save changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 7. Dispatch domain events
            await _eventDispatcher.DispatchAsync(payment.DomainEvents, cancellationToken);
            payment.ClearDomainEvents();

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"An unexpected error occurred: {ex.Message}");
        }
    }
}