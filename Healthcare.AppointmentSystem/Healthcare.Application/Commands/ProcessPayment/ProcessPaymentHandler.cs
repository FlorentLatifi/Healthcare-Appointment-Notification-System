using Healthcare.Application.Common;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Payments;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Healthcare.Application.Commands.ProcessPayment;

public sealed class ProcessPaymentHandler : ICommandHandler<ProcessPaymentCommand, Result<int>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly ILogger<ProcessPaymentHandler> _logger;

    public ProcessPaymentHandler(
        IUnitOfWork unitOfWork,
        IPaymentGateway paymentGateway,
        IDomainEventDispatcher eventDispatcher,
        ILogger<ProcessPaymentHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _paymentGateway = paymentGateway;
        _eventDispatcher = eventDispatcher;
        _logger = logger;
    }

    public async Task<Result<int>> HandleAsync(
        ProcessPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var appointment = await _unitOfWork.Appointments
                .GetByIdAsync(command.AppointmentId, cancellationToken);

            if (appointment == null)
            {
                return Result<int>.Failure($"Appointment with ID {command.AppointmentId} not found.");
            }

            var existingPayment = await _unitOfWork.Payments
                .GetByAppointmentIdAsync(command.AppointmentId, cancellationToken);

            if (existingPayment != null && existingPayment.Status == Domain.Enums.PaymentStatus.Succeeded)
            {
                return Result<int>.Failure("Payment has already been processed for this appointment.");
            }

            var confirmationResult = await _paymentGateway.ConfirmPaymentAsync(
                command.PaymentIntentId,
                cancellationToken);

            if (confirmationResult.IsFailure)
            {
                return Result<int>.Failure($"Payment confirmation failed: {confirmationResult.Error}");
            }

            var confirmation = confirmationResult.Value;

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

            if (confirmation.Succeeded)
            {
                var transactionId = TransactionId.Create(confirmation.TransactionId);
                payment.MarkAsSucceeded(transactionId, confirmation.PaymentMethod);

                try
                {
                    appointment.Confirm();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        "Payment {PaymentId} for appointment {AppointmentId} succeeded but auto-confirm failed: {Error}",
                        payment.Id,
                        appointment.Id,
                        ex.Message);
                }
            }
            else
            {
                payment.MarkAsFailed(confirmation.FailureReason ?? "Unknown error");
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _eventDispatcher.DispatchAsync(payment.DomainEvents, cancellationToken);
            payment.ClearDomainEvents();

            if (appointment.DomainEvents.Count > 0)
            {
                await _eventDispatcher.DispatchAsync(appointment.DomainEvents, cancellationToken);
                appointment.ClearDomainEvents();
            }

            return confirmation.Succeeded
                ? Result<int>.Success(payment.Id)
                : Result<int>.Failure($"Payment failed: {confirmation.FailureReason}");
    }
}
