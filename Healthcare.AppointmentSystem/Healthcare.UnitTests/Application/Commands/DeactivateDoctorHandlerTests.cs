using FluentAssertions;
using Healthcare.Application.Commands.DeactivateDoctor;
using Healthcare.Application.Common;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.Events;
using Healthcare.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Application.Commands;

public class DeactivateDoctorHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IDoctorRepository> _doctorRepoMock;
    private readonly Mock<IDomainEventDispatcher> _eventDispatcherMock;
    private readonly DeactivateDoctorHandler _handler;

    public DeactivateDoctorHandlerTests()
    {
        _doctorRepoMock = new Mock<IDoctorRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _unitOfWorkMock.Setup(u => u.Doctors).Returns(_doctorRepoMock.Object);
        _eventDispatcherMock = new Mock<IDomainEventDispatcher>();
        _handler = new DeactivateDoctorHandler(_unitOfWorkMock.Object, _eventDispatcherMock.Object);
    }

    private static Doctor CreateActiveDoctor()
    {
        var doctor = Doctor.Create(
            "Jane",
            "Smith",
            Email.Create("dr.smith@test.com"),
            PhoneNumber.Create("+355672345678"),
            "LIC-12345",
            Money.Create(100m, "USD"),
            10,
            Specialty.Cardiology);

        var prop = typeof(Healthcare.Domain.Common.Entity).GetProperty("Id",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        prop?.SetValue(doctor, 1);

        return doctor;
    }

    [Fact]
    public async Task HandleAsync_WithActiveDoctor_ShouldDeactivateAndDispatchEvent()
    {
        var doctor = CreateActiveDoctor();
        _doctorRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doctor);

        var command = new DeactivateDoctorCommand { DoctorId = 1 };

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        doctor.IsActive.Should().BeFalse();

        _doctorRepoMock.Verify(r => r.UpdateAsync(doctor, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _eventDispatcherMock.Verify(d => d.DispatchAsync(
            It.IsAny<DoctorCacheInvalidationNeededEvent>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithNonExistentDoctor_ShouldReturnFailure()
    {
        _doctorRepoMock.Setup(r => r.GetByIdAsync(9999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);

        var command = new DeactivateDoctorCommand { DoctorId = 9999 };

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        result.Error.Should().Contain("9999");

        _doctorRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Doctor>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventDispatcherMock.Verify(d => d.DispatchAsync(
            It.IsAny<DoctorCacheInvalidationNeededEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithAlreadyDeactivatedDoctor_ShouldReturnFailure()
    {
        var doctor = CreateActiveDoctor();
        doctor.Deactivate();
        _doctorRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doctor);

        var command = new DeactivateDoctorCommand { DoctorId = 1 };

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNullOrEmpty();

        _doctorRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Doctor>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventDispatcherMock.Verify(d => d.DispatchAsync(
            It.IsAny<DoctorCacheInvalidationNeededEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
