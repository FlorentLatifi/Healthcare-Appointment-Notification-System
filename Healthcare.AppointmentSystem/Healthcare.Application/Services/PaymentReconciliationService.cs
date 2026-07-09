using Healthcare.Application.Common;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Payments;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
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

        if (existingPayment?.Status == PaymentStatus.Succeeded)
        {
            return Result<int>.Success(existingPayment.Id);
        }

        var isNewPayment = existingPayment == null;
        var payment = existingPayment ?? Payment.Create(
            appointment.Id, appointment.ConsultationFee, "Stripe");

        if (isNewPayment)
        {
            await _unitOfWork.Payments.AddAsync(payment, cancellationToken);
        }

        ApplyPaymentOutcome(payment, appointment, succeeded, transactionId, paymentMethod, failureReason);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (isNewPayment && IsUniqueConstraintViolation(ex))
        {
            _logger.LogInformation(
                "Unique constraint collision on Payment.AppointmentId {AppointmentId}; retrying.",
                appointmentId);

            _unitOfWork.ResetChangeTracker();

            appointment = await _unitOfWork.Appointments
                .GetByIdAsync(appointmentId, cancellationToken);
            if (appointment == null)
            {
                return Result<int>.Failure($"Appointment with ID {appointmentId} not found.");
            }

            payment = await _unitOfWork.Payments
                .GetByAppointmentIdAsync(appointmentId, cancellationToken);
            if (payment == null)
            {
                throw; // Safety net — should not happen
            }

            ApplyPaymentOutcome(payment, appointment, succeeded, transactionId, paymentMethod, failureReason);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

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

    private static void ApplyPaymentOutcome(
        Payment payment,
        Appointment appointment,
        bool succeeded,
        string transactionId,
        string paymentMethod,
        string? failureReason)
    {
        if (succeeded)
        {
            if (payment.Status == PaymentStatus.Pending)
            {
                var txnId = TransactionId.Create(transactionId);
                payment.MarkAsSucceeded(txnId, paymentMethod);
            }

            try
            {
                appointment.Confirm();
            }
            catch (Exception ex)
            {
                // Already confirmed or in a state that can't transition — fine.
            }
        }
        else
        {
            if (payment.Status == PaymentStatus.Pending)
            {
                payment.MarkAsFailed(failureReason ?? "Unknown error");
            }
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("IX_Payments_AppointmentId") ||
               (message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) &&
                message.Contains("AppointmentId", StringComparison.OrdinalIgnoreCase));
    }
}
