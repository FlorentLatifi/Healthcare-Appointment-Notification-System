using FluentAssertions;
using Healthcare.Adapters.Events;
using Healthcare.Adapters.Persistence.InMemory;
using Healthcare.Adapters.Services;
using Healthcare.Application.Commands.ProcessPayment;
using Healthcare.Application.Commands.RefundPayment;
using Healthcare.Application.Common;
using Healthcare.Application.Observability;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Payments;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Services;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Application.Payments;

/// <summary>
/// Payment failure compensation: money + appointment state must stay consistent.
/// These tests catch "paid but unconfirmed", "failed but confirmed", and refund side-effects.
/// </summary>
public sealed class PaymentCompensationFlowTests
{
    private readonly IUnitOfWork _uow;
    private readonly IDomainEventDispatcher _dispatcher;
    private readonly PaymentReconciliationService _reconciliation;
    private readonly Mock<IPaymentGateway> _gateway = new();

    public PaymentCompensationFlowTests()
    {
        _uow = new InMemoryUnitOfWork(
            new InMemoryAppointmentRepository(),
            new InMemoryPatientRepository(),
            new InMemoryDoctorRepository(),
            new InMemoryUserRepository(),
            new InMemoryPaymentRepository(),
            new InMemoryAuditLogRepository(),
            Mock.Of<IUserSessionRepository>());

        _dispatcher = new DomainEventDispatcher(
            new ServiceCollection().BuildServiceProvider(),
            Mock.Of<ILogger<DomainEventDispatcher>>());

        _reconciliation = new PaymentReconciliationService(
            _uow, _dispatcher, Mock.Of<ILogger<PaymentReconciliationService>>());
    }

    private static (Patient Patient, Doctor Doctor) People()
    {
        var patient = Patient.Create(
            "Pay", "Patient", Email.Create("pay.patient@test.com"), PhoneNumber.Create("+38344111222"),
            new DateTime(1991, 2, 2), Gender.Female,
            Address.Create("1 St", "Pristina", "KS", "10000", "Kosovo"));
        var doctor = Doctor.Create(
            "Pay", "Doctor", Email.Create("pay.doctor@test.com"), PhoneNumber.Create("+38344999888"),
            "LIC-PAY-01", Money.Create(80, "EUR"), 8, Specialty.Cardiology);
        return (patient, doctor);
    }

    private static AppointmentTime FutureSlot()
    {
        var d = DateTime.Now.Date.AddDays(9);
        while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) d = d.AddDays(1);
        return AppointmentTime.Create(d.AddHours(10));
    }

    private async Task<Appointment> SeedPendingAppointmentAsync(DateTime? slotOverride = null)
    {
        var (patient, doctor) = People();
        await _uow.Patients.AddAsync(patient);
        await _uow.Doctors.AddAsync(doctor);
        await _uow.SaveChangesAsync();

        var slot = slotOverride is null
            ? FutureSlot()
            : AppointmentTime.Create(slotOverride.Value);

        var appointment = Appointment.Create(
            patient, doctor, slot,
            "Payment compensation flow test visit",
            new AppointmentCodeGenerator());
        appointment.ClearDomainEvents();
        await _uow.Appointments.AddAsync(appointment);
        await _uow.SaveChangesAsync();
        return appointment;
    }

    private async Task<(Appointment A, Appointment B)> SeedTwoPendingAppointmentsSamePatientAsync()
    {
        var (patient, doctor) = People();
        await _uow.Patients.AddAsync(patient);
        await _uow.Doctors.AddAsync(doctor);
        await _uow.SaveChangesAsync();

        var day = DateTime.Now.Date.AddDays(11);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) day = day.AddDays(1);

        var a = Appointment.Create(
            patient, doctor, AppointmentTime.Create(day.AddHours(9)),
            "Appointment A for rebinding test visit",
            new AppointmentCodeGenerator());
        a.ClearDomainEvents();
        await _uow.Appointments.AddAsync(a);
        await _uow.SaveChangesAsync();

        var b = Appointment.Create(
            patient, doctor, AppointmentTime.Create(day.AddHours(11)),
            "Appointment B for rebinding test visit",
            new AppointmentCodeGenerator());
        b.ClearDomainEvents();
        await _uow.Appointments.AddAsync(b);
        await _uow.SaveChangesAsync();

        return (a, b);
    }

    [Fact]
    public async Task GatewayFailure_DoesNotConfirmAppointment_AndMarksPaymentFailed()
    {
        // Essential: declined card must leave appointment Pending (no free service).
        var appointment = await SeedPendingAppointmentAsync();

        var result = await _reconciliation.ReconcilePaymentAsync(
            appointment.Id, "pi_fail_001", succeeded: false, "txn_fail_001", "card", "card_declined");

        result.IsFailure.Should().BeTrue();
        var payment = await _uow.Payments.GetByAppointmentIdAsync(appointment.Id);
        payment!.Status.Should().Be(PaymentStatus.Failed);
        payment.FailureReason.Should().Contain("card_declined");

        var updated = await _uow.Appointments.GetByIdAsync(appointment.Id);
        updated!.Status.Should().Be(AppointmentStatus.Pending);
    }

    [Fact]
    public async Task GatewaySuccess_ConfirmsPendingAppointment_AndMarksPaymentSucceeded()
    {
        // Essential: happy path compensation — payment success drives Confirm transition.
        var appointment = await SeedPendingAppointmentAsync();

        var result = await _reconciliation.ReconcilePaymentAsync(
            appointment.Id, "pi_ok_00001", succeeded: true, "txn_ok_00001", "card", null);

        result.IsSuccess.Should().BeTrue();
        (await _uow.Payments.GetByAppointmentIdAsync(appointment.Id))!.Status.Should().Be(PaymentStatus.Succeeded);
        (await _uow.Appointments.GetByIdAsync(appointment.Id))!.Status.Should().Be(AppointmentStatus.Confirmed);
    }

    [Fact]
    public async Task ProcessPaymentHandler_WhenConfirmFails_RecordsFailedMetricPath_DoesNotConfirm()
    {
        // Essential: handler-level gateway error must not call reconciliation with success.
        var appointment = await SeedPendingAppointmentAsync();
        _gateway.Setup(g => g.ConfirmPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentConfirmationResult>.Failure("stripe_timeout"));

        var handler = new ProcessPaymentHandler(
            _gateway.Object,
            _reconciliation,
            _uow,
            new BusinessMetrics(),
            Mock.Of<Healthcare.Application.Ports.Audit.IAuditLogService>(),
            Mock.Of<ILogger<ProcessPaymentHandler>>());

        var result = await handler.HandleAsync(new ProcessPaymentCommand
        {
            AppointmentId = appointment.Id,
            PaymentIntentId = "pi_timeout"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Payment confirmation failed");
        (await _uow.Appointments.GetByIdAsync(appointment.Id))!.Status.Should().Be(AppointmentStatus.Pending);
        (await _uow.Payments.GetByAppointmentIdAsync(appointment.Id)).Should().BeNull();
    }

    /// <summary>
    /// Rebinding attack: same patient owns A and B; succeeded PI metadata points at A;
    /// process against B must be rejected and B must stay unpaid/unconfirmed.
    /// </summary>
    [Fact]
    public async Task ProcessPaymentHandler_RebindingSucceededPiFromAppointmentA_ToB_IsRejected()
    {
        var (appointmentA, appointmentB) = await SeedTwoPendingAppointmentsSamePatientAsync();

        _gateway.Setup(g => g.ConfirmPaymentAsync("pi_for_appointment_A", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentConfirmationResult>.Success(new PaymentConfirmationResult
            {
                Succeeded = true,
                TransactionId = "pi_for_appointment_A",
                PaymentMethod = "card",
                AmountInCents = (long)(appointmentA.ConsultationFee.Amount * 100),
                Currency = appointmentA.ConsultationFee.Currency.ToLowerInvariant(),
                Metadata = new Dictionary<string, string>
                {
                    ["appointment_id"] = appointmentA.Id.ToString()
                }
            }));

        var handler = new ProcessPaymentHandler(
            _gateway.Object,
            _reconciliation,
            _uow,
            new BusinessMetrics(),
            Mock.Of<Healthcare.Application.Ports.Audit.IAuditLogService>(),
            Mock.Of<ILogger<ProcessPaymentHandler>>());

        var result = await handler.HandleAsync(new ProcessPaymentCommand
        {
            AppointmentId = appointmentB.Id,
            PaymentIntentId = "pi_for_appointment_A"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain($"bound to appointment {appointmentA.Id}");
        result.Error.Should().Contain($"appointment {appointmentB.Id}");

        (await _uow.Appointments.GetByIdAsync(appointmentB.Id))!.Status
            .Should().Be(AppointmentStatus.Pending);
        (await _uow.Payments.GetByAppointmentIdAsync(appointmentB.Id))
            .Should().BeNull();
        (await _uow.Appointments.GetByIdAsync(appointmentA.Id))!.Status
            .Should().Be(AppointmentStatus.Pending);
    }

    [Fact]
    public async Task ProcessPaymentHandler_MatchingBinding_ReconcilesSuccessfully()
    {
        var appointment = await SeedPendingAppointmentAsync();

        _gateway.Setup(g => g.ConfirmPaymentAsync("pi_match_binding_ok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentConfirmationResult>.Success(new PaymentConfirmationResult
            {
                Succeeded = true,
                TransactionId = "pi_match_binding_ok",
                PaymentMethod = "card",
                AmountInCents = (long)(appointment.ConsultationFee.Amount * 100),
                Currency = appointment.ConsultationFee.Currency.ToLowerInvariant(),
                Metadata = new Dictionary<string, string>
                {
                    ["appointment_id"] = appointment.Id.ToString()
                }
            }));

        var handler = new ProcessPaymentHandler(
            _gateway.Object,
            _reconciliation,
            _uow,
            new BusinessMetrics(),
            Mock.Of<Healthcare.Application.Ports.Audit.IAuditLogService>(),
            Mock.Of<ILogger<ProcessPaymentHandler>>());

        var result = await handler.HandleAsync(new ProcessPaymentCommand
        {
            AppointmentId = appointment.Id,
            PaymentIntentId = "pi_match_binding_ok"
        });

        result.IsSuccess.Should().BeTrue();
        (await _uow.Payments.GetByAppointmentIdAsync(appointment.Id))!.Status
            .Should().Be(PaymentStatus.Succeeded);
        (await _uow.Appointments.GetByIdAsync(appointment.Id))!.Status
            .Should().Be(AppointmentStatus.Confirmed);
    }

    [Fact]
    public async Task RefundSucceeded_CancelsUpcomingConfirmedAppointment()
    {
        // Essential: money returned ⇒ consultation no longer owed; cancel if still upcoming.
        var appointment = await SeedPendingAppointmentAsync();
        await _reconciliation.ReconcilePaymentAsync(
            appointment.Id, "pi_ref_0001", succeeded: true, "txn_ref_0001", "card", null);

        var payment = await _uow.Payments.GetByAppointmentIdAsync(appointment.Id);
        payment.Should().NotBeNull();

        _gateway.Setup(g => g.RefundPaymentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RefundResult>.Success(new RefundResult
            {
                RefundId = "re_3QK5ZB2eZvKYlo2C0refund",
                Status = "succeeded",
                AmountRefundedInCents = 8000
            }));

        var refundHandler = new RefundPaymentHandler(
            _uow, _gateway.Object, _dispatcher, new BusinessMetrics());

        var refund = await refundHandler.HandleAsync(new RefundPaymentCommand
        {
            PaymentId = payment!.Id,
            Reason = "Patient cancelled after paying for the visit"
        });

        refund.IsSuccess.Should().BeTrue();
        (await _uow.Payments.GetByIdAsync(payment.Id))!.Status.Should().Be(PaymentStatus.Refunded);
        (await _uow.Appointments.GetByIdAsync(appointment.Id))!.Status.Should().Be(AppointmentStatus.Cancelled);
    }

    [Fact]
    public async Task GatewaySuccess_OnAlreadyCancelledAppointment_SucceedsPayment_DoesNotUncancel()
    {
        // Essential: late webhook after cancel must not resurrect the appointment (compensation race).
        var appointment = await SeedPendingAppointmentAsync();
        appointment.Cancel("Patient cancelled before payment settled fully");
        await _uow.Appointments.UpdateAsync(appointment);
        await _uow.SaveChangesAsync();

        var result = await _reconciliation.ReconcilePaymentAsync(
            appointment.Id, "pi_late_001", succeeded: true, "txn_late_001", "card", null);

        result.IsSuccess.Should().BeTrue();
        (await _uow.Payments.GetByAppointmentIdAsync(appointment.Id))!.Status.Should().Be(PaymentStatus.Succeeded);
        (await _uow.Appointments.GetByIdAsync(appointment.Id))!.Status.Should().Be(AppointmentStatus.Cancelled);
    }

    [Fact]
    public async Task DoubleReconcileSuccess_IsIdempotent_SinglePaymentRow()
    {
        // Essential: Stripe retries webhooks — second success must not create a second payment or re-confirm.
        var appointment = await SeedPendingAppointmentAsync();

        var first = await _reconciliation.ReconcilePaymentAsync(
            appointment.Id, "pi_idem_001", succeeded: true, "txn_idem_01", "card", null);
        var second = await _reconciliation.ReconcilePaymentAsync(
            appointment.Id, "pi_idem_002", succeeded: true, "txn_idem_02", "card", null);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Value.Should().Be(first.Value);
        (await _uow.Appointments.GetByIdAsync(appointment.Id))!.Status.Should().Be(AppointmentStatus.Confirmed);
    }
}
