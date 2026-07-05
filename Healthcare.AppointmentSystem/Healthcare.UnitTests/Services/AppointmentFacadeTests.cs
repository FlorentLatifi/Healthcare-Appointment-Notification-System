using FluentAssertions;
using Healthcare.Application.Commands.BookAppointment;
using Healthcare.Application.Commands.CancelAppointment;
using Healthcare.Application.Commands.ConfirmAppointment;
using Healthcare.Application.Common;
using Healthcare.Application.Ports.Facades;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Services;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Services;

public sealed class AppointmentFacadeTests
{
    private readonly Mock<ICommandHandler<BookAppointmentCommand, Result<int>>> _bookHandlerMock;
    private readonly Mock<ICommandHandler<ConfirmAppointmentCommand, Result>> _confirmHandlerMock;
    private readonly Mock<ICommandHandler<CancelAppointmentCommand, Result>> _cancelHandlerMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly AppointmentFacade _facade;

    public AppointmentFacadeTests()
    {
        _bookHandlerMock = new Mock<ICommandHandler<BookAppointmentCommand, Result<int>>>();
        _confirmHandlerMock = new Mock<ICommandHandler<ConfirmAppointmentCommand, Result>>();
        _cancelHandlerMock = new Mock<ICommandHandler<CancelAppointmentCommand, Result>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _facade = new AppointmentFacade(
            _bookHandlerMock.Object,
            _confirmHandlerMock.Object,
            _cancelHandlerMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task ConfirmAppointmentAsync_WithoutOverride_ShouldPassDefaultsToHandler()
    {
        _confirmHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ConfirmAppointmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _facade.ConfirmAppointmentAsync(appointmentId: 1);

        result.IsSuccess.Should().BeTrue();
        _confirmHandlerMock.Verify(h => h.HandleAsync(
            It.Is<ConfirmAppointmentCommand>(c =>
                c.AppointmentId == 1 &&
                c.OverridePaymentRequirement == false &&
                c.OverrideReason == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmAppointmentAsync_WithOverride_ShouldPassOverrideToHandler()
    {
        _confirmHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ConfirmAppointmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _facade.ConfirmAppointmentAsync(
            appointmentId: 1,
            overridePaymentRequirement: true,
            overrideReason: "Emergency override reason here");

        result.IsSuccess.Should().BeTrue();
        _confirmHandlerMock.Verify(h => h.HandleAsync(
            It.Is<ConfirmAppointmentCommand>(c =>
                c.AppointmentId == 1 &&
                c.OverridePaymentRequirement == true &&
                c.OverrideReason == "Emergency override reason here"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmAppointmentAsync_WhenHandlerFails_ShouldPropagateFailure()
    {
        _confirmHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ConfirmAppointmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("Appointment not found."));

        var result = await _facade.ConfirmAppointmentAsync(appointmentId: 999);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task CancelAppointmentAsync_ShouldPassReasonToHandler()
    {
        _cancelHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CancelAppointmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _facade.CancelAppointmentAsync(
            appointmentId: 1,
            reason: "Patient requested reschedule");

        result.IsSuccess.Should().BeTrue();
        _cancelHandlerMock.Verify(h => h.HandleAsync(
            It.Is<CancelAppointmentCommand>(c =>
                c.AppointmentId == 1 &&
                c.CancellationReason == "Patient requested reschedule"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAppointmentAsync_WhenHandlerFails_ShouldPropagateFailure()
    {
        _cancelHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CancelAppointmentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("Appointment not found."));

        var result = await _facade.CancelAppointmentAsync(appointmentId: 999, reason: "N/A");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }
}
