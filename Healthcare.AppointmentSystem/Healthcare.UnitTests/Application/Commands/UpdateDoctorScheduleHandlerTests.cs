using FluentAssertions;
using Healthcare.Application.Commands.UpdateDoctorSchedule;
using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Common;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.Events;
using Healthcare.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Application.Commands;

public class UpdateDoctorScheduleHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IDoctorRepository> _doctorRepoMock;
    private readonly Mock<IDomainEventDispatcher> _eventDispatcherMock;
    private readonly UpdateDoctorScheduleHandler _handler;

    public UpdateDoctorScheduleHandlerTests()
    {
        _doctorRepoMock = new Mock<IDoctorRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _unitOfWorkMock.Setup(u => u.Doctors).Returns(_doctorRepoMock.Object);
        _eventDispatcherMock = new Mock<IDomainEventDispatcher>();
        _handler = new UpdateDoctorScheduleHandler(_unitOfWorkMock.Object, _eventDispatcherMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WithValidSchedule_UpdatesHoursAndInvalidatesCache()
    {
        var doctor = CreateDoctor(id: 5);
        _doctorRepoMock.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doctor);

        var command = new UpdateDoctorScheduleCommand
        {
            DoctorId = 5,
            WeeklySchedule = new List<WorkingHoursDto>
            {
                new() { DayOfWeek = DayOfWeek.Monday, IsWorkingDay = true, StartTime = "09:00", EndTime = "13:00" },
                new() { DayOfWeek = DayOfWeek.Tuesday, IsWorkingDay = true, StartTime = "09:00", EndTime = "13:00" },
                new() { DayOfWeek = DayOfWeek.Wednesday, IsWorkingDay = false },
                new() { DayOfWeek = DayOfWeek.Thursday, IsWorkingDay = true, StartTime = "10:00", EndTime = "16:00" },
                new() { DayOfWeek = DayOfWeek.Friday, IsWorkingDay = true, StartTime = "09:00", EndTime = "12:00" },
                new() { DayOfWeek = DayOfWeek.Saturday, IsWorkingDay = false },
                new() { DayOfWeek = DayOfWeek.Sunday, IsWorkingDay = false },
            }
        };

        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        var mon = doctor.WeeklySchedule.First(s => s.DayOfWeek == DayOfWeek.Monday);
        mon.IsWorkingDay.Should().BeTrue();
        mon.StartTime.Should().Be(new TimeOnly(9, 0));
        mon.EndTime.Should().Be(new TimeOnly(13, 0));

        var wed = doctor.WeeklySchedule.First(s => s.DayOfWeek == DayOfWeek.Wednesday);
        wed.IsWorkingDay.Should().BeFalse();

        _doctorRepoMock.Verify(r => r.UpdateAsync(doctor, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _eventDispatcherMock.Verify(
            e => e.DispatchAsync(It.IsAny<DoctorCacheInvalidationNeededEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenDoctorMissing_ReturnsFailure()
    {
        _doctorRepoMock.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);

        var result = await _handler.HandleAsync(new UpdateDoctorScheduleCommand
        {
            DoctorId = 99,
            WeeklySchedule = new List<WorkingHoursDto>
            {
                new() { DayOfWeek = DayOfWeek.Monday, IsWorkingDay = true, StartTime = "09:00", EndTime = "17:00" }
            }
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task HandleAsync_WithInvalidTimeOrder_ReturnsFailure()
    {
        var doctor = CreateDoctor(id: 1);
        _doctorRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doctor);

        var result = await _handler.HandleAsync(new UpdateDoctorScheduleCommand
        {
            DoctorId = 1,
            WeeklySchedule = new List<WorkingHoursDto>
            {
                new() { DayOfWeek = DayOfWeek.Monday, IsWorkingDay = true, StartTime = "17:00", EndTime = "09:00" }
            }
        });

        result.IsFailure.Should().BeTrue();
    }

    private static Doctor CreateDoctor(int id)
    {
        var doctor = Doctor.Create(
            "Ada",
            "Lovelace",
            Email.Create("ada@clinic.com"),
            PhoneNumber.Create("+355672345678"),
            "MED-12345",
            Money.Create(80, "USD"),
            yearsOfExperience: 10,
            Specialty.Cardiology);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(doctor, id);
        return doctor;
    }
}
