using FluentAssertions;
using Healthcare.Adapters.Services;
using Healthcare.Application.Commands.ProcessPayment;
using Healthcare.Application.Common;
using Healthcare.Application.Ports.Audit;
using Healthcare.Application.Ports.Payments;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Application.Commands;

public class ProcessPaymentHandlerTests
{
    private readonly Mock<IPaymentGateway> _paymentGatewayMock;
    private readonly Mock<IPaymentReconciliationService> _reconciliationMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IAppointmentRepository> _apptRepoMock;
    private readonly Mock<IAuditLogService> _auditMock;
    private readonly Mock<ILogger<ProcessPaymentHandler>> _loggerMock;
    private readonly ProcessPaymentHandler _handler;
    private readonly Appointment _appointment;

    public ProcessPaymentHandlerTests()
    {
        _paymentGatewayMock = new Mock<IPaymentGateway>();
        _reconciliationMock = new Mock<IPaymentReconciliationService>();
        _uowMock = new Mock<IUnitOfWork>();
        _apptRepoMock = new Mock<IAppointmentRepository>();
        _auditMock = new Mock<IAuditLogService>();
        _loggerMock = new Mock<ILogger<ProcessPaymentHandler>>();

        _uowMock.Setup(u => u.Appointments).Returns(_apptRepoMock.Object);

        _appointment = CreatePendingAppointment(appointmentId: 1);
        _apptRepoMock
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_appointment);

        _handler = new ProcessPaymentHandler(
            _paymentGatewayMock.Object,
            _reconciliationMock.Object,
            _uowMock.Object,
            new Healthcare.Application.Observability.BusinessMetrics(),
            _auditMock.Object,
            _loggerMock.Object);
    }

    private static Appointment CreatePendingAppointment(int appointmentId)
    {
        var patient = Patient.Create(
            "Pay", "Patient", Email.Create("p@test.com"), PhoneNumber.Create("+38344111000"),
            new DateTime(1990, 1, 1), Gender.Male,
            Address.Create("1 St", "City", "ST", "10000", "XK"));
        var doctor = Doctor.Create(
            "Pay", "Doctor", Email.Create("d@test.com"), PhoneNumber.Create("+38344999000"),
            "LIC-1", Money.Create(50, "EUR"), 5, Specialty.Cardiology);
        var appt = Appointment.Create(
            patient, doctor,
            AppointmentTime.Create(DateTime.Now.Date.AddDays(14).AddHours(10)),
            "Regular checkup appointment",
            new AppointmentCodeGenerator());
        typeof(Appointment).BaseType!
            .GetProperty(nameof(Appointment.Id))!
            .SetValue(appt, appointmentId);
        appt.ClearDomainEvents();
        return appt;
    }

    private static PaymentConfirmationResult SucceededBoundTo(int appointmentId, long amountCents = 5000, string currency = "eur")
        => new()
        {
            Succeeded = true,
            TransactionId = "pi_txn",
            PaymentMethod = "card",
            AmountInCents = amountCents,
            Currency = currency,
            Metadata = new Dictionary<string, string>
            {
                ["appointment_id"] = appointmentId.ToString()
            }
        };

    [Fact]
    public async Task Handle_WithPendingAppointment_AndGatewaySucceeds_ShouldReconcileAndReturnSuccess()
    {
        var appointmentId = 1;
        var paymentIntentId = "pi_test_1234567890";

        _paymentGatewayMock
            .Setup(g => g.ConfirmPaymentAsync(paymentIntentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentConfirmationResult>.Success(SucceededBoundTo(appointmentId)));

        _reconciliationMock
            .Setup(r => r.ReconcilePaymentAsync(
                appointmentId, paymentIntentId, true, "pi_txn", "card", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(42));

        var result = await _handler.HandleAsync(new ProcessPaymentCommand
        {
            AppointmentId = appointmentId,
            PaymentIntentId = paymentIntentId
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        _reconciliationMock.Verify(r => r.ReconcilePaymentAsync(
            appointmentId, paymentIntentId, true, "pi_txn", "card", null, It.IsAny<CancellationToken>()), Times.Once);
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
                TransactionId = "pi_txn",
                PaymentMethod = "card",
                FailureReason = "Insufficient funds",
                AmountInCents = 5000,
                Currency = "eur",
                Metadata = new Dictionary<string, string> { ["appointment_id"] = "1" }
            }));

        _reconciliationMock
            .Setup(r => r.ReconcilePaymentAsync(
                appointmentId, paymentIntentId, false, "pi_txn", "card", "Insufficient funds", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Failure("Payment failed: Insufficient funds"));

        var result = await _handler.HandleAsync(new ProcessPaymentCommand
        {
            AppointmentId = appointmentId,
            PaymentIntentId = paymentIntentId
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Insufficient funds");
    }

    [Fact]
    public async Task Handle_WithNonExistentAppointment_ShouldReturnFailure_WithoutGatewayCall()
    {
        _apptRepoMock
            .Setup(r => r.GetByIdAsync(9999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Appointment?)null);

        var result = await _handler.HandleAsync(new ProcessPaymentCommand
        {
            AppointmentId = 9999,
            PaymentIntentId = "pi_test_1234567890"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _paymentGatewayMock.Verify(
            g => g.ConfirmPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _reconciliationMock.Verify(
            r => r.ReconcilePaymentAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithGatewayConfirmationFailure_ShouldReturnFailure()
    {
        _paymentGatewayMock
            .Setup(g => g.ConfirmPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentConfirmationResult>.Failure("Gateway timeout"));

        var result = await _handler.HandleAsync(new ProcessPaymentCommand
        {
            AppointmentId = 1,
            PaymentIntentId = "pi_test_1234567890"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Gateway timeout");
        _reconciliationMock.Verify(
            r => r.ReconcilePaymentAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPaymentIntentBoundToDifferentAppointment_RejectsWithoutReconciling()
    {
        // Rebinding attack: succeeded PI for appointment 1 applied to appointment 2.
        var appointmentB = CreatePendingAppointment(appointmentId: 2);
        _apptRepoMock
            .Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointmentB);

        _paymentGatewayMock
            .Setup(g => g.ConfirmPaymentAsync("pi_for_appt_A", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentConfirmationResult>.Success(SucceededBoundTo(appointmentId: 1)));

        var result = await _handler.HandleAsync(new ProcessPaymentCommand
        {
            AppointmentId = 2,
            PaymentIntentId = "pi_for_appt_A"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("bound to appointment 1");
        result.Error.Should().Contain("cannot be applied to appointment 2");
        _reconciliationMock.Verify(
            r => r.ReconcilePaymentAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPaymentIntentMissingAppointmentMetadata_Rejects()
    {
        _paymentGatewayMock
            .Setup(g => g.ConfirmPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentConfirmationResult>.Success(new PaymentConfirmationResult
            {
                Succeeded = true,
                TransactionId = "pi_orphan",
                PaymentMethod = "card",
                AmountInCents = 5000,
                Currency = "eur",
                Metadata = new Dictionary<string, string>() // no appointment_id
            }));

        var result = await _handler.HandleAsync(new ProcessPaymentCommand
        {
            AppointmentId = 1,
            PaymentIntentId = "pi_orphan"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("missing appointment_id");
        _reconciliationMock.Verify(
            r => r.ReconcilePaymentAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
