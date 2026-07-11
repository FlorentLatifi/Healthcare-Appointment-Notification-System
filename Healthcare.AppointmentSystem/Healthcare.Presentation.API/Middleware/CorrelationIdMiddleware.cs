using System.Diagnostics;
using Healthcare.Application.Observability;
using Serilog.Context;

namespace Healthcare.Presentation.API.Middleware;

/// <summary>
/// Accepts or generates X-Correlation-Id, stores in HttpContext + CorrelationContext + Activity + Serilog.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        context.Items[CorrelationContext.HttpContextItemKey] = correlationId;
        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationContext.HeaderName))
                context.Response.Headers[CorrelationContext.HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using var scope = CorrelationContext.BeginScope(correlationId);

        var activity = Activity.Current;
        activity?.SetTag(CorrelationContext.TagName, correlationId);
        activity?.SetBaggage(CorrelationContext.BaggageKey, correlationId);
        activity?.SetTag("http.trace_identifier", context.TraceIdentifier);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TraceId", activity?.TraceId.ToString() ?? string.Empty))
        using (LogContext.PushProperty("SpanId", activity?.SpanId.ToString() ?? string.Empty))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationContext.HeaderName, out var header) &&
            !string.IsNullOrWhiteSpace(header))
        {
            return header.ToString().Trim();
        }

        // Prefer W3C trace id when OTel already created an activity
        if (Activity.Current is { TraceId: var traceId } && traceId != default)
            return traceId.ToString();

        return Guid.NewGuid().ToString("N");
    }
}
