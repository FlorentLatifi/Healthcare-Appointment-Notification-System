using System.Security.Claims;
using FluentAssertions;
using Healthcare.Adapters.Persistence.InMemory;
using Healthcare.Application.Commands.ProcessPayment;
using Healthcare.Application.Commands.RefundPayment;
using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Payments;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.ValueObjects;
using Healthcare.Presentation.API.Controllers;
using Healthcare.Presentation.API.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Healthcare.UnitTests.Helpers;

namespace Healthcare.UnitTests.Presentation.Controllers;

public class PaymentControllerTests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly Mock<ICommandHandler<ProcessPaymentCommand, Result<int>>> _processHandlerMock;
    private readonly Mock<ICommandHandler<RefundPaymentCommand, Result>> _refundHandlerMock;
    private readonly Mock<IPaymentGateway> _paymentGatewayMock;
    private readonly Mock<ILogger<PaymentsController>> _loggerMock;
    private readonly PaymentsController _controller;

    public PaymentControllerTests()
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

        _processHandlerMock = new Mock<ICommandHandler<ProcessPaymentCommand, Result<int>>>();
        _refundHandlerMock = new Mock<ICommandHandler<RefundPaymentCommand, Result>>();
        _paymentGatewayMock = new Mock<IPaymentGateway>();
        _loggerMock = new Mock<ILogger<PaymentsController>>();

        _controller = new PaymentsController(
            _processHandlerMock.Object,
            _refundHandlerMock.Object,
            _paymentGatewayMock.Object,
            _unitOfWork,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetPaymentById_PatientAccessesOwnPayment_Returns200()
    {
        var (_, payment) = await CreatePaymentInDbAsync();
        SetPatientPrincipal(1);

        var result = await _controller.GetPaymentById(payment.Id, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPaymentById_PatientAccessesOtherPayment_Returns403()
    {
        var (appointmentA, paymentA) = await CreatePaymentInDbAsync();
        var (_, _, patientB, _) = await CreateOtherPatientAndDoctorAsync();
        SetPatientPrincipal(patientB.Id);

        var result = await _controller.GetPaymentById(paymentA.Id, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetPaymentById_DoctorAccessesOwnPayment_Returns200()
    {
        var (ctx, payment) = await CreatePaymentInDbAsync();
        SetDoctorPrincipal(ctx.DoctorId);

        var result = await _controller.GetPaymentById(payment.Id, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPaymentById_DoctorAccessesOtherPayment_Returns403()
    {
        var (ctx, payment) = await CreatePaymentInDbAsync();
        var (otherCtx, _, _, _) = await CreateOtherPatientAndDoctorAsync();
        SetDoctorPrincipal(otherCtx.DoctorId);

        var result = await _controller.GetPaymentById(payment.Id, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetPaymentById_AdminAccessesAnyPayment_Returns200()
    {
        var (_, payment) = await CreatePaymentInDbAsync();
        SetAdminPrincipal();

        var result = await _controller.GetPaymentById(payment.Id, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPaymentByAppointment_PatientAccessesOwn_Returns200()
    {
        var (ctx, _) = await CreatePaymentInDbAsync();
        SetPatientPrincipal(ctx.PatientId);

        var result = await _controller.GetPaymentByAppointment(ctx.AppointmentId, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPaymentByAppointment_PatientAccessesOther_Returns403()
    {
        var (ctx, _) = await CreatePaymentInDbAsync();
        var (otherCtx, _, _, _) = await CreateOtherPatientAndDoctorAsync();
        SetPatientPrincipal(otherCtx.PatientId);

        var result = await _controller.GetPaymentByAppointment(ctx.AppointmentId, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetPaymentByAppointment_DoctorAccessesOwn_Returns200()
    {
        var (ctx, _) = await CreatePaymentInDbAsync();
        SetDoctorPrincipal(ctx.DoctorId);

        var result = await _controller.GetPaymentByAppointment(ctx.AppointmentId, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPaymentByAppointment_DoctorAccessesOther_Returns403()
    {
        var (ctx, _) = await CreatePaymentInDbAsync();
        var (otherCtx, _, _, _) = await CreateOtherPatientAndDoctorAsync();
        SetDoctorPrincipal(otherCtx.DoctorId);

        var result = await _controller.GetPaymentByAppointment(ctx.AppointmentId, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetPaymentByAppointment_AdminAccessesAny_Returns200()
    {
        var (ctx, _) = await CreatePaymentInDbAsync();
        SetAdminPrincipal();

        var result = await _controller.GetPaymentByAppointment(ctx.AppointmentId, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    private record PaymentContext(int PatientId, int DoctorId, int AppointmentId);

    private async Task<(PaymentContext Ctx, Payment Payment)> CreatePaymentInDbAsync()
    {
        var patient = TestDataBuilder.APatient().Build();
        var doctor = TestDataBuilder.ADoctor().Build();
        await _unitOfWork.Patients.AddAsync(patient);
        await _unitOfWork.Doctors.AddAsync(doctor);
        await _unitOfWork.SaveChangesAsync();

        var appointment = TestDataBuilder.AnAppointment()
            .WithPatient(patient)
            .WithDoctor(doctor)
            .Build();
        await _unitOfWork.Appointments.AddAsync(appointment);
        await _unitOfWork.SaveChangesAsync();

        var payment = Payment.Create(appointment.Id, Money.Create(100m, "USD"));
        await _unitOfWork.Payments.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        return (new PaymentContext(patient.Id, doctor.Id, appointment.Id), payment);
    }

    private async Task<(PaymentContext Ctx, Payment Payment, Patient Patient, Doctor Doctor)> CreateOtherPatientAndDoctorAsync()
    {
        var patientB = TestDataBuilder.APatient()
            .WithName("Other", "Patient")
            .WithEmail("other.patient@test.com")
            .Build();
        var doctorB = TestDataBuilder.ADoctor()
            .WithName("Other", "Doctor")
            .WithEmail("other.doc@test.com")
            .Build();
        await _unitOfWork.Patients.AddAsync(patientB);
        await _unitOfWork.Doctors.AddAsync(doctorB);
        await _unitOfWork.SaveChangesAsync();

        var appointmentB = TestDataBuilder.AnAppointment()
            .WithPatient(patientB)
            .WithDoctor(doctorB)
            .Build();
        await _unitOfWork.Appointments.AddAsync(appointmentB);
        await _unitOfWork.SaveChangesAsync();

        var paymentB = Payment.Create(appointmentB.Id, Money.Create(200m, "USD"));
        await _unitOfWork.Payments.AddAsync(paymentB);
        await _unitOfWork.SaveChangesAsync();

        return (new PaymentContext(patientB.Id, doctorB.Id, appointmentB.Id), paymentB, patientB, doctorB);
    }

    private void SetPatientPrincipal(int patientId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "1"),
            new(ClaimTypes.Name, "patient"),
            new(ClaimTypes.Role, "Patient"),
            new("patient_id", patientId.ToString())
        };
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };
    }

    private void SetDoctorPrincipal(int doctorId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "2"),
            new(ClaimTypes.Name, "doctor"),
            new(ClaimTypes.Role, "Doctor"),
            new("doctor_id", doctorId.ToString())
        };
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };
    }

    private void SetAdminPrincipal()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "3"),
            new(ClaimTypes.Name, "admin"),
            new(ClaimTypes.Role, "Admin")
        };
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };
    }
}
