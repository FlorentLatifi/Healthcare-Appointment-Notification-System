using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Healthcare.Application.Behaviors;

/// <summary>
/// Warns when a request exceeds a slow-threshold (default 500ms).
/// Complements <see cref="LoggingBehavior{TRequest,TResponse}"/> with a focused SLO signal.
/// </summary>
public sealed class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public const long SlowRequestThresholdMs = 500;

    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;

    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        if (sw.ElapsedMilliseconds >= SlowRequestThresholdMs)
        {
            _logger.LogWarning(
                "Slow request {RequestName} took {ElapsedMs}ms (threshold {ThresholdMs}ms)",
                typeof(TRequest).Name,
                sw.ElapsedMilliseconds,
                SlowRequestThresholdMs);
        }

        return response;
    }
}
