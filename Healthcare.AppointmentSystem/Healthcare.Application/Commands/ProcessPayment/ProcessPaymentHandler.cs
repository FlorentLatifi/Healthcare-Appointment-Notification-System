using Healthcare.Application.Common;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Payments;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.ValueObjects;

namespace Healthcare.Application.Commands.ProcessPayment;

/// <summary>
/// Handler for ProcessPaymentCommand.
/// </summary>
/// <remarks>
/// Design Pattern: Command Handler + Unit of Work
/// 
/// Responsibilities:
/// 1. Validate appointment exists
/// 2. Check payment hasn't already been processed
/// 3. Confirm payment with gateway (Stripe)
/// 4. Create Payment entity
/// 5. Auto-confirm appointment on successful payment
/// 6. Dispatch domain events
/// 
/// Transaction Boundary:
/// All database operations happen in a single transaction.
/// If payment succeeds but DB save fails → payment is still captured
/// (we'd need idempotency to handle this properly).
/// </remarks>
public sealed class ProcessPaymentHandler : ICommandHandler<ProcessPaymentCommand, Result<int>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public ProcessPaymentHandler(
        IUnitOfWork unitOfWork,
        IPaymentGateway paymentGateway,
        IDomainEventDispatcher eventDispatcher)
    {
        _unitOfWork = unitOfWork;
        _paymentGateway = paymentGateway;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<Result<int>> HandleAsync(
        ProcessPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Fetch appointment
            var appointment = await _unitOfWork.Appointments
                .GetByIdAsync(command.AppointmentId, cancellationToken);

            if (appointment == null)
            {
                return Result<int>.Failure($"Appointment with ID {command.AppointmentId} not found.");
            }

            // 2. Check if payment already exists
            var existingPayment = await _unitOfWork.Payments
                .GetByAppointmentIdAsync(command.AppointmentId, cancellationToken);

            if (existingPayment != null && existingPayment.Status == Domain.Enums.PaymentStatus.Succeeded)
            {
                return Result<int>.Failure("Payment has already been processed for this appointment.");
            }

            // 3. Confirm payment with gateway
            var confirmationResult = await _paymentGateway.ConfirmPaymentAsync(
                command.PaymentIntentId,
                cancellationToken);

            if (confirmationResult.IsFailure)
            {
                return Result<int>.Failure($"Payment confirmation failed: {confirmationResult.Error}");
            }

            var confirmation = confirmationResult.Value;

            // 4. Create or update payment entity
            Payment payment;

            if (existingPayment == null)
            {
                payment = Payment.Create(
                    appointment.Id,
                    appointment.ConsultationFee,
                    "Stripe");

                await _unitOfWork.Payments.AddAsync(payment, cancellationToken);
            }
            else
            {
                payment = existingPayment;
            }

            // 5. Update payment status based on gateway response
            if (confirmation.Succeeded)
            {
                var transactionId = TransactionId.Create(confirmation.TransactionId);
                payment.MarkAsSucceeded(transactionId, confirmation.PaymentMethod);

                // Auto-confirm appointment
                try
                {
                    appointment.Confirm();
                }
                catch (Exception ex)
                {
                    // Payment succeeded but appointment confirmation failed
                    // Log this for manual intervention
                    return Result<int>.Failure(
                        $"Payment succeeded but appointment confirmation failed: {ex.Message}");
                }
            }
            else
            {
                payment.MarkAsFailed(confirmation.FailureReason ?? "Unknown error");
            }

            // 6. Save changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 7. Dispatch domain events
            await _eventDispatcher.DispatchAsync(payment.DomainEvents, cancellationToken);
            payment.ClearDomainEvents();

            await _eventDispatcher.DispatchAsync(appointment.DomainEvents, cancellationToken);
            appointment.ClearDomainEvents();

            return confirmation.Succeeded
                ? Result<int>.Success(payment.Id)
                : Result<int>.Failure($"Payment failed: {confirmation.FailureReason}");
        }
        catch (Exception ex)
        {
            return Result<int>.Failure($"An unexpected error occurred: {ex.Message}");
        }
    }
}