using FluentAssertions;
using Healthcare.Application.Behaviors;
using Healthcare.Application.Common;
using Healthcare.Application.Ports.Audit;
using Healthcare.Domain.Audit;
using Healthcare.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Healthcare.UnitTests.Application.Audit;

public sealed class AuditLoggingBehaviorTests
{
    public sealed class SampleAuditable : IRequest<Result<int>>, IAuditableRequest
    {
        public int PatientId { get; init; }
        public string AuditAction => AuditActions.BookAppointment;
        public string AuditResourceType => "Appointment";
        public int? AuditResourceId => null;
        public object GetAuditDetails() => new { PatientId };
        public int? ResolveResourceId(object? response)
            => response is Result<int> r && r.IsSuccess ? r.Value : null;
    }

    [Fact]
    public async Task Handle_OnSuccess_WritesSuccessAuditWithResolvedResourceId()
    {
        var audit = new Mock<IAuditLogService>();
        var behavior = new AuditLoggingBehavior<SampleAuditable, Result<int>>(
            audit.Object,
            NullLogger<AuditLoggingBehavior<SampleAuditable, Result<int>>>.Instance);

        var request = new SampleAuditable { PatientId = 3 };
        RequestHandlerDelegate<Result<int>> next = () => Task.FromResult(Result<int>.Success(99));

        var result = await behavior.Handle(request, next, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        audit.Verify(a => a.WriteAsync(
            AuditActions.BookAppointment,
            "Appointment",
            99,
            AuditOutcome.Success,
            It.IsAny<object?>(),
            null,
            null,
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OnFailureResult_WritesFailureAudit()
    {
        var audit = new Mock<IAuditLogService>();
        var behavior = new AuditLoggingBehavior<SampleAuditable, Result<int>>(
            audit.Object,
            NullLogger<AuditLoggingBehavior<SampleAuditable, Result<int>>>.Instance);

        var request = new SampleAuditable { PatientId = 3 };
        RequestHandlerDelegate<Result<int>> next = () => Task.FromResult(Result<int>.Failure("slot taken"));

        var result = await behavior.Handle(request, next, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        audit.Verify(a => a.WriteAsync(
            AuditActions.BookAppointment,
            "Appointment",
            null,
            AuditOutcome.Failure,
            It.IsAny<object?>(),
            null,
            null,
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NonAuditable_DoesNotWrite()
    {
        var audit = new Mock<IAuditLogService>();
        var behavior = new AuditLoggingBehavior<string, string>(
            audit.Object,
            NullLogger<AuditLoggingBehavior<string, string>>.Instance);

        RequestHandlerDelegate<string> next = () => Task.FromResult("ok");
        var result = await behavior.Handle("plain", next, CancellationToken.None);

        result.Should().Be("ok");
        audit.Verify(a => a.WriteAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int?>(),
            It.IsAny<AuditOutcome>(),
            It.IsAny<object?>(),
            It.IsAny<int?>(),
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
