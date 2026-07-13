using FluentAssertions;
using Healthcare.Application.Commands.ProcessPayment;
using Healthcare.Application.Common;
using Healthcare.Application.Ports.Audit;
using Healthcare.Application.Ports.Payments;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Application.Commands;

public class ProcessPaymentHandlerTests
{
    private readonly Mock<IPaymentGateway> _paymentGatewayMock;
    private readonly Mock<IPaymentReconciliationService> _reconciliationMock;
    private readonly Mock<IAuditLogService> _auditMock;
    private readonly Mock<ILogger<ProcessPaymentHandler>> _loggerMock;
    private readonly ProcessPaymentHandler _handler;

    public ProcessPaymentHandlerTests()
    {
        _paymentGatewayMock = new Mock<IPaymentGateway>();
        _reconciliationMock = new Mock<IPaymentReconciliationService>();
        _auditMock = new Mock<IAuditLogService>();
        _loggerMock = new Mock<ILogger<ProcessPaymentHandler>>();

        _handler = new ProcessPaymentHandler(
            _paymentGatewayMock.Object,
            _reconciliationMock.Object,
            new Healthcare.Application.Observability.BusinessMetrics(),
            _auditMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithPendingAppointment_AndGatewaySucceeds_ShouldReconcileAndReturnSuccess()
    {
        var appointmentId = 1;
        var paymentIntentId = "pi_test_1234567890";

        _paymentGatewayMock
            .Setup(g => g.ConfirmPaymentAsync(paymentIntentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentConfirmationResult>.Success(new PaymentConfirmationResult
            {
                Succeeded = true,
                TransactionId = "txn_test_1234567890",
                PaymentMethod = "card"
            }));

        _reconciliationMock
            .Setup(r => r.ReconcilePaymentAsync(
                appointmentId, paymentIntentId, true, "txn_test_1234567890", "card", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(42));

        var command = new ProcessPaymentCommand
        {
            AppointmentId = appointmentId,
            PaymentIntentId = paymentIntentId
        };

        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task Handle_WithPaymentFailure_ShouldReturnFailure()
    {
        var appointmentId = 1;
        var paymentIntentId = "pi_test_1234567890";

        _paymentGatewayMock
            .Setup(g => g.ConfirmPaymentAsync(paymentIntentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentConfirmationResult>.Success(new PaymentConfirmationResult
            {
                Succeeded = false,
                TransactionId = "txn_test_1234567890",
                PaymentMethod = "card",
                FailureReason = "Insufficient funds"
            }));

        _reconciliationMock
            .Setup(r => r.ReconcilePaymentAsync(
                appointmentId, paymentIntentId, false, "txn_test_1234567890", "card", "Insufficient funds", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Failure("Payment failed: Insufficient funds"));

        var command = new ProcessPaymentCommand
        {
            AppointmentId = appointmentId,
            PaymentIntentId = paymentIntentId
        };

        var result = await _handler.HandleAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Insufficient funds");
    }

    [Fact]
    public async Task Handle_WithNonExistentAppointment_ShouldReturnFailure()
    {
        var appointmentId = 9999;
        var paymentIntentId = "pi_test_1234567890";

        _paymentGatewayMock
            .Setup(g => g.ConfirmPaymentAsync(paymentIntentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentConfirmationResult>.Success(new PaymentConfirmationResult
            {
                Succeeded = true,
                TransactionId = "txn_test_1234567890",
                PaymentMethod = "card"
            }));

        _reconciliationMock
            .Setup(r => r.ReconcilePaymentAsync(
                appointmentId, paymentIntentId, true, "txn_test_1234567890", "card", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Failure("Appointment with ID 9999 not found."));

        var command = new ProcessPaymentCommand
        {
            AppointmentId = appointmentId,
            PaymentIntentId = paymentIntentId
        };

        var result = await _handler.HandleAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WithGatewayConfirmationFailure_ShouldReturnFailure()
    {
        _paymentGatewayMock
            .Setup(g => g.ConfirmPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentConfirmationResult>.Failure("Gateway timeout"));

        var command = new ProcessPaymentCommand
        {
            AppointmentId = 1,
            PaymentIntentId = "pi_test_1234567890"
        };

        var result = await _handler.HandleAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Gateway timeout");
    }
}
