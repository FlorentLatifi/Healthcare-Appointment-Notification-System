using Healthcare.Application.Common;

namespace Healthcare.Application.Ports.Payments;

public interface IPaymentReconciliationService
{
    Task<Result<int>> ReconcilePaymentAsync(
        int appointmentId,
        string paymentIntentId,
        bool succeeded,
        string transactionId,
        string paymentMethod,
        string? failureReason,
        CancellationToken cancellationToken = default);
}
