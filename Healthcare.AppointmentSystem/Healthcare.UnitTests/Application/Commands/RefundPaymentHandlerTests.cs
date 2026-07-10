using FluentAssertions;
using Healthcare.Adapters.Events;
using Healthcare.Adapters.Persistence.InMemory;
using Healthcare.Application.Commands.RefundPayment;
using Healthcare.Application.Common;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Payments;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Adapters.Services;
using Healthcare.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Application.Commands;

public class RefundPaymentHandlerTests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly Mock<IPaymentGateway> _paymentGatewayMock;
    private readonly RefundPaymentHandler _handler;

    public RefundPaymentHandlerTests()
    {
        var appointmentRepo = new InMemoryAppointmentRepository();
        var patientRepo = new InMemoryPatientRepository();
        var doctorRepo = new InMemoryDoctorRepository();
        var userRepo = new InMemoryUserRepository();
        var paymentRepo = new InMemoryPaymentRepository();
        var auditLogRepo = new InMemoryAuditLogRepository();

        _unitOfWork = new InMemoryUnitOfWork(
            appointmentRepo,
            patientRepo,
            doctorRepo,
            userRepo,
            paymentRepo,
            auditLogRepo,
            Mock.Of<IUserSessionRepository>());

        var mockLogger = new Mock<ILogger<DomainEventDispatcher>>();
        var serviceProvider = CreateServiceProvider();
        _eventDispatcher = new DomainEventDispatcher(serviceProvider, mockLogger.Object);

        _paymentGatewayMock = new Mock<IPaymentGateway>();
        _handler = new RefundPaymentHandler(_unitOfWork, _paymentGatewayMock.Object, _eventDispatcher);
    }

    #region Helper Methods

    private static Patient CreateTestPatient()
    {
        var email = Email.Create("patient@test.com");
        var phone = PhoneNumber.Create("+38349123456");
        var address = Address.Create("123 Main St", "Pristina", "Kosovo", "10000", "Kosovo");

        return Patient.Create(
            "John",
            "Doe",
            email,
            phone,
            new DateTime(1990, 1, 1),
            Gender.Male,
            address);
    }

    private static Doctor CreateTestDoctor()
    {
        var email = Email.Create("doctor@test.com");
        var phone = PhoneNumber.Create("+38349987654");
        var fee = Money.Create(50, "EUR");

        return Doctor.Create(
            "Jane",
            "Smith",
            email,
            phone,
            "LIC-12345",
            fee,
            10,
            Specialty.GeneralPractice);
    }

    private static AppointmentTime CreateFutureAppointmentTime()
    {
        var futureDate = DateTime.Now.AddDays(7).Date;

        while (futureDate.DayOfWeek == DayOfWeek.Saturday ||
               futureDate.DayOfWeek == DayOfWeek.Sunday)
        {
            futureDate = futureDate.AddDays(1);
        }

        return AppointmentTime.Create(futureDate.AddHours(10));
    }

    private async Task<(Appointment Appointment, Payment Payment)> CreateSucceededPaymentWithAppointmentAsync(
        string currency = "EUR",
        AppointmentStatus appointmentStatus = AppointmentStatus.Confirmed,
        bool usePastTime = false)
    {
        var patient = CreateTestPatient();
        var doctor = CreateTestDoctor();

        await _unitOfWork.Patients.AddAsync(patient);
        await _unitOfWork.Doctors.AddAsync(doctor);
        await _unitOfWork.SaveChangesAsync();

        var scheduledTime = CreateFutureAppointmentTime();
        var appointment = Appointment.Create(
            patient,
            doctor,
            scheduledTime,
            "Annual checkup and consultation",
            new AppointmentCodeGenerator());

        appointment.Confirm();

        if (usePastTime)
        {
            var pastTime = DateTime.UtcNow.AddDays(-1).Date.AddHours(10);
            var ctor = typeof(AppointmentTime).GetConstructor(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null,
                new[] { typeof(DateTime) },
                null);
            var pastAppointmentTime = (AppointmentTime)ctor!.Invoke(new object[] { pastTime });
            var field = typeof(Appointment).GetField(
                "<ScheduledTime>k__BackingField",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field!.SetValue(appointment, pastAppointmentTime);
        }

        if (appointmentStatus == AppointmentStatus.Cancelled)
        {
            appointment.Cancel("Patient requested cancellation due to scheduling conflict");
        }
        else if (appointmentStatus == AppointmentStatus.Completed)
        {
            appointment.Complete("Examination completed successfully with no issues found.");
        }

        appointment.ClearDomainEvents();

        await _unitOfWork.Appointments.AddAsync(appointment);
        await _unitOfWork.SaveChangesAsync();

        var money = Money.Create(50, currency);
        var payment = Payment.Create(appointment.Id, money);
        var transactionId = TransactionId.Create("pi_3QK5ZB2eZvKYlo2C0X8Z5X6Y");
        payment.MarkAsSucceeded(transactionId, "card");
        payment.ClearDomainEvents();

        await _unitOfWork.Payments.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        return (appointment, payment);
    }

    private async Task<Payment> CreatePaymentAsync(PaymentStatus status, string currency = "EUR")
    {
        var patient = CreateTestPatient();
        var doctor = CreateTestDoctor();

        await _unitOfWork.Patients.AddAsync(patient);
        await _unitOfWork.Doctors.AddAsync(doctor);
        await _unitOfWork.SaveChangesAsync();

        var scheduledTime = CreateFutureAppointmentTime();
        var appointment = Appointment.Create(
            patient,
            doctor,
            scheduledTime,
            "Annual checkup and consultation",
            new AppointmentCodeGenerator());

        appointment.Confirm();
        appointment.ClearDomainEvents();

        await _unitOfWork.Appointments.AddAsync(appointment);
        await _unitOfWork.SaveChangesAsync();

        var money = Money.Create(50, currency);
        var payment = Payment.Create(appointment.Id, money);

        if (status == PaymentStatus.Succeeded)
        {
            var transactionId = TransactionId.Create("pi_3QK5ZB2eZvKYlo2C0X8Z5X6Y");
            payment.MarkAsSucceeded(transactionId, "card");
        }
        else if (status == PaymentStatus.Failed)
        {
            payment.MarkAsFailed("Card declined");
        }
        else if (status == PaymentStatus.Refunded)
        {
            var transactionId = TransactionId.Create("pi_3QK5ZB2eZvKYlo2C0X8Z5X6Y");
            payment.MarkAsSucceeded(transactionId, "card");
            payment.InitiateRefund();
            payment.CompleteRefund(TransactionId.Create("re_3QK5ZB2eZvKYlo2C0X8Z5X6Y"));
        }

        payment.ClearDomainEvents();

        await _unitOfWork.Payments.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        return payment;
    }

    private void SetupGatewaySuccess()
    {
        _paymentGatewayMock
            .Setup(x => x.RefundPaymentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<decimal?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RefundResult>.Success(new RefundResult
            {
                RefundId = "re_3QK5ZB2eZvKYlo2C0X8Z5X6Y",
                Status = "succeeded",
                AmountRefundedInCents = 5000
            }));
    }

    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        return services.BuildServiceProvider();
    }

    #endregion

    #region Happy Path Tests

    [Fact]
    public async Task Handle_WithSucceededPaymentAndConfirmedAppointment_ShouldRefundAndCancelAppointment()
    {
        var (appointment, payment) = await CreateSucceededPaymentWithAppointmentAsync();
        SetupGatewaySuccess();

        var command = new RefundPaymentCommand
        {
            PaymentId = payment.Id,
            Reason = "Patient requested cancellation"
        };

        var result = await _handler.HandleAsync(command);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        var refundedPayment = await _unitOfWork.Payments.GetByIdAsync(payment.Id);
        refundedPayment.Should().NotBeNull();
        refundedPayment!.Status.Should().Be(PaymentStatus.Refunded);
        refundedPayment.RefundTransactionId.Should().NotBeNull();

        var updatedAppointment = await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);
        updatedAppointment.Should().NotBeNull();
        updatedAppointment!.Status.Should().Be(AppointmentStatus.Cancelled);
        updatedAppointment.CancellationReason.Should().Be("Payment refunded");
    }

    [Fact]
    public async Task Handle_ShouldDispatchAndClearDomainEvents()
    {
        var (_, payment) = await CreateSucceededPaymentWithAppointmentAsync();
        SetupGatewaySuccess();

        var command = new RefundPaymentCommand
        {
            PaymentId = payment.Id,
            Reason = "Patient requested cancellation"
        };

        await _handler.HandleAsync(command);

        var refundedPayment = await _unitOfWork.Payments.GetByIdAsync(payment.Id);
        refundedPayment!.DomainEvents.Should().BeEmpty();

        var appointment = await _unitOfWork.Appointments.GetByIdAsync(refundedPayment.AppointmentId);
        appointment!.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task Handle_WithNonExistentPayment_ShouldReturnFailure()
    {
        var command = new RefundPaymentCommand
        {
            PaymentId = 9999,
            Reason = "Patient requested cancellation"
        };

        var result = await _handler.HandleAsync(command);

        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        result.Error.Should().Contain("9999");
    }

    [Fact]
    public async Task Handle_WithRefundedPayment_ShouldReturnFailureAndNotCallGateway()
    {
        var payment = await CreatePaymentAsync(PaymentStatus.Refunded);

        var command = new RefundPaymentCommand
        {
            PaymentId = payment.Id,
            Reason = "Trying to refund already refunded payment"
        };

        var result = await _handler.HandleAsync(command);

        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Refunded");

        _paymentGatewayMock.Verify(
            x => x.RefundPaymentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<decimal?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithFailedPayment_ShouldReturnFailureAndNotCallGateway()
    {
        var payment = await CreatePaymentAsync(PaymentStatus.Failed);

        var command = new RefundPaymentCommand
        {
            PaymentId = payment.Id,
            Reason = "Trying to refund failed payment"
        };

        var result = await _handler.HandleAsync(command);

        result.Should().NotBeNull();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed");

        _paymentGatewayMock.Verify(
            x => x.RefundPaymentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<decimal?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Currency Tests

    [Fact]
    public async Task Handle_ShouldPassPaymentCurrencyToGateway()
    {
        var (_, payment) = await CreateSucceededPaymentWithAppointmentAsync(currency: "EUR");
        SetupGatewaySuccess();

        var command = new RefundPaymentCommand
        {
            PaymentId = payment.Id,
            Reason = "Patient requested cancellation"
        };

        await _handler.HandleAsync(command);

        _paymentGatewayMock.Verify(
            x => x.RefundPaymentAsync(
                It.IsAny<string>(),
                It.Is<string>(c => c == "EUR"),
                It.IsAny<decimal?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonUsdCurrency_ShouldPassCorrectCurrencyToGateway()
    {
        var (_, payment) = await CreateSucceededPaymentWithAppointmentAsync(currency: "JPY");
        SetupGatewaySuccess();

        var command = new RefundPaymentCommand
        {
            PaymentId = payment.Id,
            Reason = "Patient requested cancellation"
        };

        await _handler.HandleAsync(command);

        _paymentGatewayMock.Verify(
            x => x.RefundPaymentAsync(
                It.IsAny<string>(),
                It.Is<string>(c => c == "JPY"),
                It.IsAny<decimal?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Appointment State Tests

    [Fact]
    public async Task Handle_WithCompletedAppointment_ShouldRefundButNotCancelAppointment()
    {
        var (appointment, payment) = await CreateSucceededPaymentWithAppointmentAsync(
            appointmentStatus: AppointmentStatus.Completed);
        SetupGatewaySuccess();

        var command = new RefundPaymentCommand
        {
            PaymentId = payment.Id,
            Reason = "Patient requested cancellation"
        };

        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();

        var refundedPayment = await _unitOfWork.Payments.GetByIdAsync(payment.Id);
        refundedPayment!.Status.Should().Be(PaymentStatus.Refunded);

        var updatedAppointment = await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);
        updatedAppointment!.Status.Should().Be(AppointmentStatus.Completed);
    }

    [Fact]
    public async Task Handle_WithCancelledAppointment_ShouldRefundButNotCancelAppointment()
    {
        var (appointment, payment) = await CreateSucceededPaymentWithAppointmentAsync(
            appointmentStatus: AppointmentStatus.Cancelled);
        SetupGatewaySuccess();

        var command = new RefundPaymentCommand
        {
            PaymentId = payment.Id,
            Reason = "Patient requested cancellation"
        };

        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();

        var updatedAppointment = await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);
        updatedAppointment!.Status.Should().Be(AppointmentStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_WithPastAppointment_ShouldRefundButNotCancelAppointment()
    {
        var (appointment, payment) = await CreateSucceededPaymentWithAppointmentAsync(usePastTime: true);
        SetupGatewaySuccess();

        var command = new RefundPaymentCommand
        {
            PaymentId = payment.Id,
            Reason = "Patient requested cancellation"
        };

        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();

        var refundedPayment = await _unitOfWork.Payments.GetByIdAsync(payment.Id);
        refundedPayment!.Status.Should().Be(PaymentStatus.Refunded);

        var updatedAppointment = await _unitOfWork.Appointments.GetByIdAsync(appointment.Id);
        updatedAppointment!.Status.Should().Be(AppointmentStatus.Confirmed);
    }

    #endregion
}
