using FluentAssertions;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Queries.Analytics;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Application.Queries.Analytics;

public class GetRevenueReportHandlerTests
{
    private readonly Mock<IPaymentRepository> _paymentRepo;
    private readonly GetRevenueReportHandler _handler;

    public GetRevenueReportHandlerTests()
    {
        _paymentRepo = new Mock<IPaymentRepository>();
        _handler = new GetRevenueReportHandler(_paymentRepo.Object);
    }

    [Fact]
    public async Task Handle_WithDateRange_ShouldReturnTotalRevenue()
    {
        var from = new DateTime(2026, 1, 1);
        var to = new DateTime(2026, 1, 31);
        _paymentRepo.Setup(r => r.GetTotalRevenueAsync(from, to, default))
            .ReturnsAsync(1500.00m);

        var query = new GetRevenueReportQuery(from, to);
        var result = await _handler.HandleAsync(query);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalRevenue.Should().Be(1500.00m);
        result.Value.Currency.Should().Be("USD");
        result.Value.DateFrom.Should().Be(from);
        result.Value.DateTo.Should().Be(to);
        result.Value.ByDoctor.Should().BeNull();
        result.Value.BySpecialty.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithGroupByDoctor_ShouldIncludeDoctorBreakdown()
    {
        var from = new DateTime(2026, 1, 1);
        var to = new DateTime(2026, 1, 31);
        _paymentRepo.Setup(r => r.GetTotalRevenueAsync(from, to, default))
            .ReturnsAsync(1000.00m);
        _paymentRepo.Setup(r => r.GetRevenueByDoctorAsync(from, to, default))
            .ReturnsAsync(new List<DoctorRevenueResult>
            {
                new(1, "Jane", "Smith", 600.00m),
                new(2, "John", "Doe", 400.00m)
            });

        var query = new GetRevenueReportQuery(from, to, "doctor");
        var result = await _handler.HandleAsync(query);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalRevenue.Should().Be(1000.00m);
        result.Value.ByDoctor.Should().HaveCount(2);
        result.Value.ByDoctor![0].DoctorName.Should().Be("Dr. Jane Smith");
        result.Value.ByDoctor[0].Revenue.Should().Be(600.00m);
        result.Value.ByDoctor[1].DoctorName.Should().Be("Dr. John Doe");
        result.Value.ByDoctor[1].Revenue.Should().Be(400.00m);
    }

    [Fact]
    public async Task Handle_WithGroupBySpecialty_ShouldIncludeSpecialtyBreakdown()
    {
        var from = new DateTime(2026, 1, 1);
        var to = new DateTime(2026, 1, 31);
        _paymentRepo.Setup(r => r.GetTotalRevenueAsync(from, to, default))
            .ReturnsAsync(1000.00m);
        _paymentRepo.Setup(r => r.GetRevenueBySpecialtyAsync(from, to, default))
            .ReturnsAsync(new List<SpecialtyRevenueResult>
            {
                new("GeneralPractice", 600.00m),
                new("Cardiology", 400.00m)
            });

        var query = new GetRevenueReportQuery(from, to, "specialty");
        var result = await _handler.HandleAsync(query);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalRevenue.Should().Be(1000.00m);
        result.Value.BySpecialty.Should().HaveCount(2);
        result.Value.BySpecialty![0].Specialty.Should().Be("GeneralPractice");
        result.Value.BySpecialty[0].Revenue.Should().Be(600.00m);
    }

    [Fact]
    public async Task Handle_WithEmptyDateRange_ShouldReturnZeroRevenue()
    {
        var from = new DateTime(2025, 1, 1);
        var to = new DateTime(2025, 1, 2);
        _paymentRepo.Setup(r => r.GetTotalRevenueAsync(from, to, default))
            .ReturnsAsync(0.00m);

        var query = new GetRevenueReportQuery(from, to);
        var result = await _handler.HandleAsync(query);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalRevenue.Should().Be(0.00m);
        result.Value.ByDoctor.Should().BeNull();
        result.Value.BySpecialty.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithEmptyDateRangeAndGroupBy_ShouldReturnEmptyBreakdowns()
    {
        var from = new DateTime(2025, 1, 1);
        var to = new DateTime(2025, 1, 2);
        _paymentRepo.Setup(r => r.GetTotalRevenueAsync(from, to, default))
            .ReturnsAsync(0.00m);
        _paymentRepo.Setup(r => r.GetRevenueByDoctorAsync(from, to, default))
            .ReturnsAsync(new List<DoctorRevenueResult>());

        var query = new GetRevenueReportQuery(from, to, "doctor");
        var result = await _handler.HandleAsync(query);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalRevenue.Should().Be(0.00m);
        result.Value.ByDoctor.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_ShouldPropagateException()
    {
        var from = new DateTime(2026, 1, 1);
        var to = new DateTime(2026, 1, 31);
        _paymentRepo.Setup(r => r.GetTotalRevenueAsync(from, to, default))
            .ThrowsAsync(new Exception("DB connection failed"));

        var query = new GetRevenueReportQuery(from, to);
        Func<Task> act = () => _handler.HandleAsync(query);

        await act.Should().ThrowAsync<Exception>().WithMessage("DB connection failed");
    }
}
