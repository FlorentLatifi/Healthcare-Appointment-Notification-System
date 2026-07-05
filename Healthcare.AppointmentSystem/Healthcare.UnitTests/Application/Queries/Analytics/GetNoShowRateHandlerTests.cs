using FluentAssertions;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Queries.Analytics;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Application.Queries.Analytics;

public class GetNoShowRateHandlerTests
{
    private readonly Mock<IAppointmentRepository> _apptRepo;
    private readonly GetNoShowRateHandler _handler;

    public GetNoShowRateHandlerTests()
    {
        _apptRepo = new Mock<IAppointmentRepository>();
        _handler = new GetNoShowRateHandler(_apptRepo.Object);
    }

    [Fact]
    public async Task Handle_WithAppointments_ShouldCalculateCorrectRate()
    {
        var from = new DateTime(2026, 1, 1);
        var to = new DateTime(2026, 1, 31);
        _apptRepo.Setup(r => r.GetStatusCountsAsync(from, to, default))
            .ReturnsAsync(new StatusCountsResult(10, 20, 15, 5, 5));

        var result = await _handler.HandleAsync(new GetNoShowRateQuery(from, to));

        result.IsSuccess.Should().BeTrue();
        result.Value.NoShowCount.Should().Be(5);
        result.Value.ConfirmedCount.Should().Be(20);
        result.Value.CompletedCount.Should().Be(15);
        result.Value.TotalCount.Should().Be(40);
        result.Value.NoShowRatePercent.Should().Be(12.5);
    }

    [Fact]
    public async Task Handle_WithNoAppointments_ShouldReturnZeroRate()
    {
        var from = new DateTime(2025, 1, 1);
        var to = new DateTime(2025, 1, 31);
        _apptRepo.Setup(r => r.GetStatusCountsAsync(from, to, default))
            .ReturnsAsync(new StatusCountsResult(0, 0, 0, 0, 0));

        var result = await _handler.HandleAsync(new GetNoShowRateQuery(from, to));

        result.IsSuccess.Should().BeTrue();
        result.Value.NoShowCount.Should().Be(0);
        result.Value.ConfirmedCount.Should().Be(0);
        result.Value.CompletedCount.Should().Be(0);
        result.Value.TotalCount.Should().Be(0);
        result.Value.NoShowRatePercent.Should().Be(0.0);
    }

    [Fact]
    public async Task Handle_WithOnlyNoShows_ShouldReturn100Percent()
    {
        var from = new DateTime(2026, 1, 1);
        var to = new DateTime(2026, 1, 31);
        _apptRepo.Setup(r => r.GetStatusCountsAsync(from, to, default))
            .ReturnsAsync(new StatusCountsResult(0, 0, 0, 0, 10));

        var result = await _handler.HandleAsync(new GetNoShowRateQuery(from, to));

        result.IsSuccess.Should().BeTrue();
        result.Value.NoShowCount.Should().Be(10);
        result.Value.NoShowRatePercent.Should().Be(100.0);
    }

    [Fact]
    public async Task Handle_WithNoRelevantAppointments_ShouldReturnZeroRate()
    {
        var from = new DateTime(2026, 1, 1);
        var to = new DateTime(2026, 1, 31);
        _apptRepo.Setup(r => r.GetStatusCountsAsync(from, to, default))
            .ReturnsAsync(new StatusCountsResult(5, 0, 0, 0, 0));

        var result = await _handler.HandleAsync(new GetNoShowRateQuery(from, to));

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(0);
        result.Value.NoShowRatePercent.Should().Be(0.0);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_ShouldPropagateException()
    {
        _apptRepo.Setup(r => r.GetStatusCountsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ThrowsAsync(new Exception("DB error"));

        Func<Task> act = () => _handler.HandleAsync(new GetNoShowRateQuery(DateTime.MinValue, DateTime.MaxValue));

        await act.Should().ThrowAsync<Exception>().WithMessage("DB error");
    }
}
