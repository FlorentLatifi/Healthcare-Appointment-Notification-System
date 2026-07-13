using Healthcare.Application.Common;
using Healthcare.Application.Ports.Audit;
using Healthcare.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Healthcare.Application.Behaviors;

/// <summary>
/// Automatically writes an immutable audit row for MediatR requests implementing
/// <see cref="IAuditableRequest"/>. Runs after the handler so outcome and resource id are known.
/// Failures in audit persistence do not fail the business operation.
/// </summary>
public sealed class AuditLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AuditLoggingBehavior<TRequest, TResponse>> _logger;

    public AuditLoggingBehavior(
        IAuditLogService auditLogService,
        ILogger<AuditLoggingBehavior<TRequest, TResponse>> logger)
    {
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IAuditableRequest auditable)
            return await next();

        TResponse response;
        try
        {
            response = await next();
        }
        catch (Exception ex)
        {
            await SafeWriteAsync(
                auditable,
                AuditOutcome.Failure,
                response: null,
                error: ex.Message,
                cancellationToken);
            throw;
        }

        var outcome = IsFailureResult(response) ? AuditOutcome.Failure : AuditOutcome.Success;
        var error = TryGetError(response);
        await SafeWriteAsync(auditable, outcome, response, error, cancellationToken);
        return response;
    }

    private async Task SafeWriteAsync(
        IAuditableRequest auditable,
        AuditOutcome outcome,
        object? response,
        string? error,
        CancellationToken cancellationToken)
    {
        try
        {
            var resourceId = response is null
                ? auditable.AuditResourceId
                : auditable.ResolveResourceId(response) ?? auditable.AuditResourceId;

            var details = auditable.GetAuditDetails();
            if (outcome == AuditOutcome.Failure && !string.IsNullOrWhiteSpace(error))
            {
                details = new { details, error };
            }

            await _auditLogService.WriteAsync(
                auditable.AuditAction,
                auditable.AuditResourceType,
                resourceId,
                outcome,
                details,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AuditLoggingBehavior failed for {Action}", auditable.AuditAction);
        }
    }

    private static bool IsFailureResult(object? response)
    {
        return response switch
        {
            Result r => r.IsFailure,
            _ => false
        };
    }

    private static string? TryGetError(object? response)
    {
        return response switch
        {
            Result r when r.IsFailure => r.Error,
            _ => null
        };
    }
}
