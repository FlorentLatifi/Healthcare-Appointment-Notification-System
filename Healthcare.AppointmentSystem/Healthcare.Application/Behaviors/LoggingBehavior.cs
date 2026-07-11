using System.Diagnostics;
using Healthcare.Application.Observability;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Healthcare.Application.Behaviors;

/// <summary>
/// Structured MediatR logging with correlation id and activity tags.
/// Outer-most pipeline behavior.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var correlationId = CorrelationContext.Current ?? CorrelationContext.GetOrCreate();

        using var activity = HealthcareActivitySource.Instance.StartActivity(
            $"mediatr.{requestName}",
            ActivityKind.Internal);

        activity?.SetTag("mediatr.request", requestName);
        activity?.SetTag(CorrelationContext.TagName, correlationId);
        activity?.SetBaggage(CorrelationContext.BaggageKey, correlationId);

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RequestName"] = requestName,
            ["TraceId"] = Activity.Current?.TraceId.ToString() ?? string.Empty
        }))
        {
            _logger.LogInformation(
                "Handling {RequestName} CorrelationId={CorrelationId}",
                requestName,
                correlationId);

            var sw = Stopwatch.StartNew();
            try
            {
                var response = await next();
                sw.Stop();
                _logger.LogInformation(
                    "Handled {RequestName} in {ElapsedMs}ms CorrelationId={CorrelationId} Outcome=Success",
                    requestName,
                    sw.ElapsedMilliseconds,
                    correlationId);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return response;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(
                    ex,
                    "Error handling {RequestName} after {ElapsedMs}ms CorrelationId={CorrelationId} Outcome=Error",
                    requestName,
                    sw.ElapsedMilliseconds,
                    correlationId);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddEvent(new ActivityEvent(
                    "exception",
                    tags: new ActivityTagsCollection
                    {
                        { "exception.type", ex.GetType().FullName },
                        { "exception.message", ex.Message }
                    }));
                throw;
            }
        }
    }
}
