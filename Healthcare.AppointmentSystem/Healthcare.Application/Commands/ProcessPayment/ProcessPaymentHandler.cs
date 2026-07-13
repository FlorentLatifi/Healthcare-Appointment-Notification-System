using Healthcare.Application.Common;
using Healthcare.Application.Observability;
using Healthcare.Application.Ports.Audit;
using Healthcare.Application.Ports.Payments;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Services;
using Healthcare.Domain.Audit;
using Healthcare.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Healthcare.Application.Commands.ProcessPayment;

/// <summary>
/// Reconciles a client-confirmed Stripe PaymentIntent against a local appointment.
/// Does not charge the card; enforces PaymentIntent → appointment binding to prevent rebinding attacks.
/// </summary>
public sealed class ProcessPaymentHandler : ICommandHandler<ProcessPaymentCommand, Result<int>>
{
    private readonly IPaymentGateway _paymentGateway;
    private readonly IPaymentReconciliationService _reconciliationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBusinessMetrics _metrics;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<ProcessPaymentHandler> _logger;

    public ProcessPaymentHandler(
        IPaymentGateway paymentGateway,
        IPaymentReconciliationService reconciliationService,
        IUnitOfWork unitOfWork,
        IBusinessMetrics metrics,
        IAuditLogService auditLogService,
        ILogger<ProcessPaymentHandler> logger)
    {
        _paymentGateway = paymentGateway;
        _reconciliationService = reconciliationService;
        _unitOfWork = unitOfWork;
        _metrics = metrics;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<Result<int>> HandleAsync(
        ProcessPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Load appointment (needed for fee match + clear error if missing).
        var appointment = await _unitOfWork.Appointments
            .GetByIdAsync(command.AppointmentId, cancellationToken);

        if (appointment is null)
        {
            await WriteAuditFailureAsync(
                command, "appointment_not_found", cancellationToken);
            return Result<int>.Failure(
                $"Appointment with ID {command.AppointmentId} not found.");
        }

        // 2. Retrieve PI status + metadata from gateway (no charge here).
        var confirmationResult = await _paymentGateway.ConfirmPaymentAsync(
            command.PaymentIntentId,
            cancellationToken);

        if (confirmationResult.IsFailure)
        {
            _metrics.PaymentFailed("confirmation_failed");
            _logger.LogWarning(
                "Payment intent retrieve failed Intent={PaymentIntentId} CorrelationId={CorrelationId}",
                command.PaymentIntentId,
                CorrelationContext.Current);

            await WriteAuditFailureAsync(
                command, "gateway_retrieve_failed", cancellationToken);

            return Result<int>.Failure(
                $"Payment confirmation failed: {confirmationResult.Error}");
        }

        var confirmation = confirmationResult.Value;

        // 3. Bind PI to this appointment (blocks rebinding attacks).
        var binding = PaymentIntentBinding.Validate(
            confirmation,
            command.AppointmentId,
            expectedAmount: appointment.ConsultationFee.Amount,
            expectedCurrency: appointment.ConsultationFee.Currency);

        if (binding.IsFailure)
        {
            _metrics.PaymentFailed("intent_rebinding_rejected");
            _logger.LogWarning(
                "Rejected PaymentIntent rebinding Intent={PaymentIntentId} " +
                "TargetAppointment={AppointmentId} BoundAppointment={BoundAppointmentId} " +
                "CorrelationId={CorrelationId} Reason={Reason}",
                command.PaymentIntentId,
                command.AppointmentId,
                confirmation.BoundAppointmentId,
                CorrelationContext.Current,
                binding.Error);

            await WriteAuditFailureAsync(
                command, "intent_rebinding_rejected", cancellationToken);

            return Result<int>.Failure(binding.Error);
        }

        // 4. Apply local payment + appointment state transitions.
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
                BoundAppointmentId = confirmation.BoundAppointmentId,
                PaymentMethod = confirmation.PaymentMethod
            },
            cancellationToken: cancellationToken);

        return result;
    }

    private Task WriteAuditFailureAsync(
        ProcessPaymentCommand command,
        string error,
        CancellationToken cancellationToken)
        => _auditLogService.WriteAsync(
            AuditActions.ProcessPayment,
            "Payment",
            resourceId: null,
            AuditOutcome.Failure,
            details: new
            {
                command.AppointmentId,
                PaymentIntentIdPrefix = Prefix(command.PaymentIntentId),
                Error = error
            },
            cancellationToken: cancellationToken);

    private static string Prefix(string intentId)
        => string.IsNullOrEmpty(intentId)
            ? string.Empty
            : intentId.Length <= 12 ? intentId : intentId[..12] + "…";
}
