using System.Security.Claims;
using FluentAssertions;
using Healthcare.Adapters.Persistence.InMemory;
using Healthcare.Application.Commands.CancelAppointment;
using Healthcare.Application.Commands.CompleteAppointment;
using Healthcare.Application.Commands.ConfirmAppointment;
using Healthcare.Application.Commands.MarkNoShowAppointment;
using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Mappings;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Queries.GetAppointment;
using Healthcare.Domain.Entities;
using Healthcare.Presentation.API.Controllers;
using Healthcare.Presentation.API.Requests;
using Healthcare.UnitTests.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Presentation.Controllers;

public class AppointmentControllerTests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ICommandHandler<CompleteAppointmentCommand, Result>> _completeHandlerMock;
    private readonly Mock<ICommandHandler<MarkNoShowAppointmentCommand, Result>> _markNoShowHandlerMock;
    private readonly Mock<ICommandHandler<CancelAppointmentCommand, Result>> _cancelHandlerMock;
    private readonly Mock<ILogger<AppointmentsController>> _loggerMock;
    private readonly Mock<IDomainEventDispatcher> _eventDispatcherMock;
    private readonly AppointmentsController _controller;

    public AppointmentControllerTests()
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

        _mediatorMock = new Mock<IMediator>();
        _completeHandlerMock = new Mock<ICommandHandler<CompleteAppointmentCommand, Result>>();
        _markNoShowHandlerMock = new Mock<ICommandHandler<MarkNoShowAppointmentCommand, Result>>();
        _cancelHandlerMock = new Mock<ICommandHandler<CancelAppointmentCommand, Result>>();
        _loggerMock = new Mock<ILogger<AppointmentsController>>();
        _eventDispatcherMock = new Mock<IDomainEventDispatcher>();

        _controller = new AppointmentsController(
            _mediatorMock.Object,
            _cancelHandlerMock.Object,
            _completeHandlerMock.Object,
            _markNoShowHandlerMock.Object,
            _unitOfWork,
            _loggerMock.Object,
            _eventDispatcherMock.Object);
    }

    [Fact]
    public async Task GetAppointmentById_PatientAccessingOwnAppointment_Returns200()
    {
        var (appointment, patient, _) = await CreateAppointmentInDbAsync();
        var dto = AppointmentMapper.ToDto(appointment);
        SetupGetAppointment(dto);
        SetPatientPrincipal(patient.Id);

        var result = await _controller.GetAppointmentById(appointment.Id, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetAppointmentById_PatientAccessingOtherAppointment_Returns403()
    {
        var (appointmentA, patientA, _) = await CreateAppointmentInDbAsync();
        var (_, patientB, _) = await CreateAnotherPatientAndDoctorAsync();
        var dto = AppointmentMapper.ToDto(appointmentA);
        SetupGetAppointment(dto);
        SetPatientPrincipal(patientB.Id);

        var result = await _controller.GetAppointmentById(appointmentA.Id, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetAppointmentById_DoctorAccessingOwnAppointment_Returns200()
    {
        var (appointment, _, doctor) = await CreateAppointmentInDbAsync();
        var dto = AppointmentMapper.ToDto(appointment);
        SetupGetAppointment(dto);
        SetDoctorPrincipal(doctor.Id);

        var result = await _controller.GetAppointmentById(appointment.Id, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetAppointmentById_DoctorAccessingOtherAppointment_Returns403()
    {
        var (appointmentA, _, doctorA) = await CreateAppointmentInDbAsync();
        var (_, _, doctorB) = await CreateAnotherPatientAndDoctorAsync();
        var dto = AppointmentMapper.ToDto(appointmentA);
        SetupGetAppointment(dto);
        SetDoctorPrincipal(doctorB.Id);

        var result = await _controller.GetAppointmentById(appointmentA.Id, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetAppointmentById_AdminAccessingAnyAppointment_Returns200()
    {
        var (appointment, _, _) = await CreateAppointmentInDbAsync();
        var dto = AppointmentMapper.ToDto(appointment);
        SetupGetAppointment(dto);
        SetAdminPrincipal();

        var result = await _controller.GetAppointmentById(appointment.Id, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CancelAppointment_PatientCancelsOwn_Returns200()
    {
        var (appointment, patient, _) = await CreateAppointmentInDbAsync();
        SetPatientPrincipal(patient.Id);
        _cancelHandlerMock.Setup(h => h.HandleAsync(It.IsAny<CancelAppointmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _controller.CancelAppointment(appointment.Id,
            new CancelAppointmentRequest { CancellationReason = "Changed my mind" }, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CancelAppointment_PatientCancelsOther_Returns403()
    {
        var (appointmentA, patientA, _) = await CreateAppointmentInDbAsync();
        var (_, patientB, _) = await CreateAnotherPatientAndDoctorAsync();
        SetPatientPrincipal(patientB.Id);

        var result = await _controller.CancelAppointment(appointmentA.Id,
            new CancelAppointmentRequest { CancellationReason = "Not mine" }, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task CancelAppointment_DoctorCancelsOwn_Returns200()
    {
        var (appointment, _, doctor) = await CreateAppointmentInDbAsync();
        SetDoctorPrincipal(doctor.Id);
        _cancelHandlerMock.Setup(h => h.HandleAsync(It.IsAny<CancelAppointmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _controller.CancelAppointment(appointment.Id,
            new CancelAppointmentRequest { CancellationReason = "Doctor unavailable" }, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CancelAppointment_DoctorCancelsOther_Returns403()
    {
        var (appointmentA, _, doctorA) = await CreateAppointmentInDbAsync();
        var (_, _, doctorB) = await CreateAnotherPatientAndDoctorAsync();
        SetDoctorPrincipal(doctorB.Id);

        var result = await _controller.CancelAppointment(appointmentA.Id,
            new CancelAppointmentRequest { CancellationReason = "Not my patient" }, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task CancelAppointment_AdminCancelsAny_Returns200()
    {
        var (appointment, _, _) = await CreateAppointmentInDbAsync();
        SetAdminPrincipal();
        _cancelHandlerMock.Setup(h => h.HandleAsync(It.IsAny<CancelAppointmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _controller.CancelAppointment(appointment.Id,
            new CancelAppointmentRequest { CancellationReason = "Admin override" }, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ConfirmAppointment_DoctorConfirmsOwn_Returns200()
    {
        var (appointment, _, doctor) = await CreateAppointmentInDbAsync();
        SetDoctorPrincipal(doctor.Id);
        SetupConfirmSuccess();

        var result = await _controller.ConfirmAppointment(appointment.Id,
            new ConfirmAppointmentRequest(), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ConfirmAppointment_DoctorConfirmsOther_Returns403()
    {
        var (appointmentA, _, doctorA) = await CreateAppointmentInDbAsync();
        var (_, _, doctorB) = await CreateAnotherPatientAndDoctorAsync();
        SetDoctorPrincipal(doctorB.Id);

        var result = await _controller.ConfirmAppointment(appointmentA.Id,
            new ConfirmAppointmentRequest(), CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task ConfirmAppointment_AdminConfirmsAny_Returns200()
    {
        var (appointment, _, _) = await CreateAppointmentInDbAsync();
        SetAdminPrincipal();
        SetupConfirmSuccess();

        var result = await _controller.ConfirmAppointment(appointment.Id,
            new ConfirmAppointmentRequest { OverridePaymentRequirement = true }, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CompleteAppointment_DoctorCompletesOwn_Returns200()
    {
        var (appointment, _, doctor) = await CreateAppointmentInDbAsync();
        SetDoctorPrincipal(doctor.Id);
        _completeHandlerMock.Setup(h => h.HandleAsync(It.IsAny<CompleteAppointmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _controller.CompleteAppointment(appointment.Id,
            new CompleteAppointmentRequest { DoctorNotes = "All good" }, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CompleteAppointment_DoctorCompletesOther_Returns403()
    {
        var (appointmentA, _, doctorA) = await CreateAppointmentInDbAsync();
        var (_, _, doctorB) = await CreateAnotherPatientAndDoctorAsync();
        SetDoctorPrincipal(doctorB.Id);

        var result = await _controller.CompleteAppointment(appointmentA.Id,
            new CompleteAppointmentRequest { DoctorNotes = "Not my patient" }, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task CompleteAppointment_AdminCompletesAny_Returns200()
    {
        var (appointment, _, _) = await CreateAppointmentInDbAsync();
        SetAdminPrincipal();
        _completeHandlerMock.Setup(h => h.HandleAsync(It.IsAny<CompleteAppointmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _controller.CompleteAppointment(appointment.Id,
            new CompleteAppointmentRequest { DoctorNotes = "Admin notes" }, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task MarkNoShow_DoctorMarksOwn_Returns200()
    {
        var (appointment, _, doctor) = await CreateAppointmentInDbAsync();
        SetDoctorPrincipal(doctor.Id);
        _markNoShowHandlerMock.Setup(h => h.HandleAsync(It.IsAny<MarkNoShowAppointmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _controller.MarkNoShowAppointment(appointment.Id, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task MarkNoShow_DoctorMarksOther_Returns403()
    {
        var (appointmentA, _, doctorA) = await CreateAppointmentInDbAsync();
        var (_, _, doctorB) = await CreateAnotherPatientAndDoctorAsync();
        SetDoctorPrincipal(doctorB.Id);

        var result = await _controller.MarkNoShowAppointment(appointmentA.Id, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task MarkNoShow_AdminMarksAny_Returns200()
    {
        var (appointment, _, _) = await CreateAppointmentInDbAsync();
        SetAdminPrincipal();
        _markNoShowHandlerMock.Setup(h => h.HandleAsync(It.IsAny<MarkNoShowAppointmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _controller.MarkNoShowAppointment(appointment.Id, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetAppointmentsByPatient_PatientAccessesOwn_Returns200()
    {
        var (_, patient, _) = await CreateAppointmentInDbAsync();
        SetPatientPrincipal(patient.Id);

        var result = await _controller.GetAppointmentsByPatient(patient.Id, 1, 20, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetAppointmentsByPatient_PatientAccessesOther_Returns403()
    {
        var (_, patientA, _) = await CreateAppointmentInDbAsync();
        var (_, patientB, _) = await CreateAnotherPatientAndDoctorAsync();
        SetPatientPrincipal(patientB.Id);

        var result = await _controller.GetAppointmentsByPatient(patientA.Id, 1, 20, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetAppointmentsByDoctor_DoctorAccessesOwn_Returns200()
    {
        var (_, _, doctor) = await CreateAppointmentInDbAsync();
        SetDoctorPrincipal(doctor.Id);

        var result = await _controller.GetAppointmentsByDoctor(doctor.Id, 1, 20, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetAppointmentsByDoctor_DoctorAccessesOther_Returns403()
    {
        var (_, _, doctorA) = await CreateAppointmentInDbAsync();
        var (_, _, doctorB) = await CreateAnotherPatientAndDoctorAsync();
        SetDoctorPrincipal(doctorB.Id);

        var result = await _controller.GetAppointmentsByDoctor(doctorA.Id, 1, 20, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    private void SetupGetAppointment(AppointmentDto dto)
    {
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAppointmentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AppointmentDto>.Success(dto));
    }

    private void SetupConfirmSuccess()
    {
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ConfirmAppointmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
    }

    private async Task<(Appointment Appointment, Patient Patient, Doctor Doctor)> CreateAppointmentInDbAsync()
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

        return (appointment, patient, doctor);
    }

    private async Task<(Appointment Appointment, Patient Patient, Doctor Doctor)> CreateAnotherPatientAndDoctorAsync()
    {
        var patientB = TestDataBuilder.APatient()
            .WithName("Jane", "Doe")
            .WithEmail("jane.doe@test.com")
            .Build();
        var doctorB = TestDataBuilder.ADoctor()
            .WithName("Dr", "Jones")
            .WithEmail("dr.jones@test.com")
            .Build();
        await _unitOfWork.Patients.AddAsync(patientB);
        await _unitOfWork.Doctors.AddAsync(doctorB);
        await _unitOfWork.SaveChangesAsync();

        return (null!, patientB, doctorB);
    }

    private void SetPatientPrincipal(int patientId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "patient"),
            new Claim(ClaimTypes.Role, "Patient"),
            new Claim("patient_id", patientId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    private void SetDoctorPrincipal(int doctorId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "2"),
            new Claim(ClaimTypes.Name, "doctor"),
            new Claim(ClaimTypes.Role, "Doctor"),
            new Claim("doctor_id", doctorId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    private void SetAdminPrincipal()
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "3"),
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }
}
