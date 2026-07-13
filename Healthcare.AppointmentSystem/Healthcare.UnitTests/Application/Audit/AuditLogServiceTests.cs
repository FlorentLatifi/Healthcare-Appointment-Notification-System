using FluentAssertions;
using Healthcare.Application.Ports.Audit;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Services;
using Healthcare.Domain.Audit;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;

namespace Healthcare.UnitTests.Application.Audit;

public sealed class AuditLogServiceTests
{
    private readonly Mock<IAuditLogRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IAuditContext> _ctx = new();
    private readonly AuditLogService _sut;

    public AuditLogServiceTests()
    {
        _ctx.SetupGet(c => c.ActorUserId).Returns(42);
        _ctx.SetupGet(c => c.ActorRole).Returns("Admin");
        _ctx.SetupGet(c => c.ClientIp).Returns("10.0.0.8");
        _ctx.SetupGet(c => c.CorrelationId).Returns("corr-1");
        _ctx.SetupGet(c => c.UserAgent).Returns("UnitTest/1.0");

        _sut = new AuditLogService(
            _repo.Object,
            _uow.Object,
            _ctx.Object,
            Mock.Of<ILogger<AuditLogService>>());
    }

    [Fact]
    public async Task WriteAsync_PersistsAppendOnlyEntry_WithActorIpAndOutcome()
    {
        AuditLogEntry? captured = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<AuditLogEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogEntry, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        await _sut.WriteAsync(
            AuditActions.GetPatientById,
            "Patient",
            resourceId: 7,
            AuditOutcome.Success,
            details: new { Via = "test" });

        captured.Should().NotBeNull();
        captured!.EventType.Should().Be(AuditActions.GetPatientById);
        captured.EntityType.Should().Be("Patient");
        captured.EntityId.Should().Be(7);
        captured.UserId.Should().Be(42);
        captured.ActorRole.Should().Be("Admin");
        captured.Outcome.Should().Be("Success");
        captured.ClientIp.Should().Be("10.0.0.8");
        captured.CorrelationId.Should().Be("corr-1");
        captured.UserAgent.Should().Be("UnitTest/1.0");
        captured.Details.Should().Contain("test");

        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WriteAsync_OnRepositoryFailure_DoesNotThrow_ByDefault()
    {
        _repo.Setup(r => r.AddAsync(It.IsAny<AuditLogEntry>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var act = () => _sut.WriteAsync(
            AuditActions.BookAppointment,
            "Appointment",
            null,
            AuditOutcome.Failure);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task WriteAsync_OnRepositoryFailure_ThrowsWhenRequested()
    {
        _repo.Setup(r => r.AddAsync(It.IsAny<AuditLogEntry>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var act = () => _sut.WriteAsync(
            AuditActions.BookAppointment,
            "Appointment",
            null,
            AuditOutcome.Failure,
            throwOnFailure: true);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
