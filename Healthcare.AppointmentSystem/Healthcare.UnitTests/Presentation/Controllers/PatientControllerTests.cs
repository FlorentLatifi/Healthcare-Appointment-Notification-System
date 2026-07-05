using FluentAssertions;
using Healthcare.Adapters.Persistence.InMemory;
using Healthcare.Application.Commands.CreatePatient;
using Healthcare.Application.Common;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Healthcare.Presentation.API.Controllers;
using Healthcare.Presentation.API.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Presentation.Controllers;

public class PatientControllerTests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PatientsController _controller;

    public PatientControllerTests()
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
            auditLogRepo);

        var handlerMock = new Mock<ICommandHandler<CreatePatientCommand, Result<int>>>();
        var localizerMock = new Mock<IStringLocalizer<Messages>>();
        var loggerMock = new Mock<ILogger<PatientsController>>();

        _controller = new PatientsController(
            handlerMock.Object,
            _unitOfWork,
            localizerMock.Object,
            loggerMock.Object);
    }

    [Fact]
    public async Task DeletePatient_WithActivePatient_Returns204AndSetsIsActiveFalse()
    {
        var patient = await CreateActivePatientAsync();

        var result = await _controller.DeletePatient(patient.Id, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();

        var stored = await _unitOfWork.Patients.GetByIdAsync(patient.Id);
        stored.Should().NotBeNull();
        stored!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeletePatient_WithActivePatient_RecordStillExistsInDatabase()
    {
        var patient = await CreateActivePatientAsync();

        await _controller.DeletePatient(patient.Id, CancellationToken.None);

        var stored = await _unitOfWork.Patients.GetByIdAsync(patient.Id);
        stored.Should().NotBeNull();
        stored!.Id.Should().Be(patient.Id);
        stored.FirstName.Should().Be(patient.FirstName);
        stored.LastName.Should().Be(patient.LastName);
        stored.Email.Value.Should().Be(patient.Email.Value);
    }

    [Fact]
    public async Task DeletePatient_WithAlreadyDeactivatedPatient_Returns400()
    {
        var patient = await CreateActivePatientAsync();

        await _controller.DeletePatient(patient.Id, CancellationToken.None);

        var result = await _controller.DeletePatient(patient.Id, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task DeletePatient_WithNonExistentPatient_Returns404()
    {
        var result = await _controller.DeletePatient(9999, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    private async Task<Patient> CreateActivePatientAsync()
    {
        var email = Email.Create("patient@test.com");
        var phone = PhoneNumber.Create("+38349123456");
        var address = Address.Create("123 Main St", "Pristina", "Kosovo", "10000", "Kosovo");

        var patient = Patient.Create(
            "John",
            "Doe",
            email,
            phone,
            new DateTime(1990, 1, 1),
            Gender.Male,
            address);

        await _unitOfWork.Patients.AddAsync(patient);
        await _unitOfWork.SaveChangesAsync();

        return patient;
    }
}
