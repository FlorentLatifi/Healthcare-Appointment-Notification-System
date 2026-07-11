using Healthcare.Application.Common;
using Healthcare.Application.Observability;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Payments;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
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
    private readonly IBusinessMetrics _metrics;

    public RefundPaymentHandler(
        IUnitOfWork unitOfWork,
        IPaymentGateway paymentGateway,
        IDomainEventDispatcher eventDispatcher,
        IBusinessMetrics metrics)
    {
        _unitOfWork = unitOfWork;
        _paymentGateway = paymentGateway;
        _eventDispatcher = eventDispatcher;
        _metrics = metrics;
    }

    public async Task<Result> HandleAsync(
        RefundPaymentCommand command,
        CancellationToken cancellationToken = default)
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
            payment.Amount.Currency,
            reason: command.Reason,
            cancellationToken: cancellationToken);

        if (refundResult.IsFailure)
        {
            return Result.Failure($"Refund failed: {refundResult.Error}");
        }

        // 5. Complete refund
        var refundTransactionId = TransactionId.Create(refundResult.Value.RefundId);
        payment.CompleteRefund(refundTransactionId);

        // 6. Sync the appointment status with the refunded payment.
        //    A refund means the consultation is no longer paid for, so if the
        //    appointment is still upcoming and Confirmed, it must be cancelled.
        //    We intentionally do NOT touch Pending/Completed/Cancelled/NoShow —
        //    those states are either not yet confirmed, already resolved, or
        //    the appointment already happened.
        Appointment? appointment = null;

        appointment = await _unitOfWork.Appointments
            .GetByIdAsync(payment.AppointmentId, cancellationToken);

        if (appointment is not null
            && appointment.Status == AppointmentStatus.Confirmed
            && !appointment.ScheduledTime.IsPast())
        {
            appointment.Cancel("Payment refunded");
            await _unitOfWork.Appointments.UpdateAsync(appointment, cancellationToken);
        }

        // 7. Save changes (payment + appointment together, same transaction)
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 8. Dispatch domain events
        await _eventDispatcher.DispatchAsync(payment.DomainEvents, cancellationToken);
        payment.ClearDomainEvents();

        if (appointment is not null && appointment.DomainEvents.Count > 0)
        {
            await _eventDispatcher.DispatchAsync(appointment.DomainEvents, cancellationToken);
            appointment.ClearDomainEvents();
        }

        _metrics.PaymentRefunded();
        return Result.Success();
    }
}