using FluentAssertions;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Queries.Analytics;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Application.Queries.Analytics;

public class GetAppointmentVolumeHandlerTests
{
    private readonly Mock<IAppointmentRepository> _apptRepo;
    private readonly GetAppointmentVolumeHandler _handler;

    public GetAppointmentVolumeHandlerTests()
    {
        _apptRepo = new Mock<IAppointmentRepository>();
        _handler = new GetAppointmentVolumeHandler(_apptRepo.Object);
    }

    [Fact]
    public async Task Handle_WithDailyGrouping_ShouldReturnDailyVolume()
    {
        var from = new DateTime(2026, 1, 1);
        var to = new DateTime(2026, 1, 3);
        _apptRepo.Setup(r => r.GetDailyVolumeAsync(from, to, default))
            .ReturnsAsync(new List<DailyVolumeResult>
            {
                new(new DateTime(2026, 1, 1), 5, 3, 1),
                new(new DateTime(2026, 1, 2), 7, 4, 2)
            });

        var query = new GetAppointmentVolumeQuery(from, to, "day");
        var result = await _handler.HandleAsync(query);

        result.IsSuccess.Should().BeTrue();
        result.Value.GroupBy.Should().Be("day");
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items[0].Period.Should().Be("2026-01-01");
        result.Value.Items[0].Created.Should().Be(5);
        result.Value.Items[0].Confirmed.Should().Be(3);
        result.Value.Items[0].Cancelled.Should().Be(1);
        result.Value.Items[1].Period.Should().Be("2026-01-02");
        result.Value.Items[1].Created.Should().Be(7);
    }

    [Fact]
    public async Task Handle_WithDefaultGroupBy_ShouldUseDay()
    {
        var from = new DateTime(2026, 1, 1);
        var to = new DateTime(2026, 1, 2);
        _apptRepo.Setup(r => r.GetDailyVolumeAsync(from, to, default))
            .ReturnsAsync(new List<DailyVolumeResult>
            {
                new(new DateTime(2026, 1, 1), 3, 2, 0)
            });

        var query = new GetAppointmentVolumeQuery(from, to);
        var result = await _handler.HandleAsync(query);

        result.IsSuccess.Should().BeTrue();
        result.Value.GroupBy.Should().Be("day");
        result.Value.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WithWeeklyGrouping_ShouldReturnWeeklyVolume()
    {
        var from = new DateTime(2026, 1, 1);
        var to = new DateTime(2026, 1, 31);
        _apptRepo.Setup(r => r.GetWeeklyVolumeAsync(from, to, default))
            .ReturnsAsync(new List<WeeklyVolumeResult>
            {
                new(2026, 1, 10, 6, 2),
                new(2026, 2, 15, 10, 3)
            });

        var query = new GetAppointmentVolumeQuery(from, to, "week");
        var result = await _handler.HandleAsync(query);

        result.IsSuccess.Should().BeTrue();
        result.Value.GroupBy.Should().Be("week");
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items[0].Period.Should().Be("2026-W01");
        result.Value.Items[0].Created.Should().Be(10);
        result.Value.Items[0].Confirmed.Should().Be(6);
        result.Value.Items[0].Cancelled.Should().Be(2);
        result.Value.Items[1].Period.Should().Be("2026-W02");
    }

    [Fact]
    public async Task Handle_WithEmptyDateRange_ShouldReturnEmptyList()
    {
        var from = new DateTime(2025, 1, 1);
        var to = new DateTime(2025, 1, 2);
        _apptRepo.Setup(r => r.GetDailyVolumeAsync(from, to, default))
            .ReturnsAsync(new List<DailyVolumeResult>());

        var query = new GetAppointmentVolumeQuery(from, to, "day");
        var result = await _handler.HandleAsync(query);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_ShouldReturnFailure()
    {
        _apptRepo.Setup(r => r.GetDailyVolumeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ThrowsAsync(new Exception("DB error"));

        var result = await _handler.HandleAsync(new GetAppointmentVolumeQuery(DateTime.MinValue, DateTime.MaxValue));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("unexpected error");
    }
}
