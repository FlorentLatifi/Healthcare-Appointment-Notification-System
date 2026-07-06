using Healthcare.Application.Common;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Payments;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Healthcare.Application.Services;

public sealed class PaymentReconciliationService : IPaymentReconciliationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly ILogger<PaymentReconciliationService> _logger;

    public PaymentReconciliationService(
        IUnitOfWork unitOfWork,
        IDomainEventDispatcher eventDispatcher,
        ILogger<PaymentReconciliationService> logger)
    {
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
        _logger = logger;
    }

    public async Task<Result<int>> ReconcilePaymentAsync(
        int appointmentId,
        string paymentIntentId,
        bool succeeded,
        string transactionId,
        string paymentMethod,
        string? failureReason,
        CancellationToken cancellationToken = default)
    {
        var appointment = await _unitOfWork.Appointments
            .GetByIdAsync(appointmentId, cancellationToken);

        if (appointment == null)
        {
            return Result<int>.Failure($"Appointment with ID {appointmentId} not found.");
        }

        var existingPayment = await _unitOfWork.Payments
            .GetByAppointmentIdAsync(appointmentId, cancellationToken);

        if (existingPayment != null && existingPayment.Status == Domain.Enums.PaymentStatus.Succeeded)
        {
            return Result<int>.Success(existingPayment.Id);
        }

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

        if (succeeded)
        {
            var txnId = TransactionId.Create(transactionId);
            payment.MarkAsSucceeded(txnId, paymentMethod);

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
            payment.MarkAsFailed(failureReason ?? "Unknown error");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _eventDispatcher.DispatchAsync(payment.DomainEvents, cancellationToken);
        payment.ClearDomainEvents();

        if (appointment.DomainEvents.Count > 0)
        {
            await _eventDispatcher.DispatchAsync(appointment.DomainEvents, cancellationToken);
            appointment.ClearDomainEvents();
        }

        return succeeded
            ? Result<int>.Success(payment.Id)
            : Result<int>.Failure($"Payment failed: {failureReason}");
    }
}
