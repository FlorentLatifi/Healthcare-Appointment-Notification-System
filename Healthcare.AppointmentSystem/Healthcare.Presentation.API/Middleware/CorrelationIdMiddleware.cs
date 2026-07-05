using Serilog.Context;
using System.Diagnostics;

namespace Healthcare.Presentation.API.Middleware;

public sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(CorrelationIdHeader, out var headerValue)
            ? headerValue.ToString()
            : Guid.NewGuid().ToString("N");

        context.Response.Headers.TryAdd(CorrelationIdHeader, correlationId);

        if (Activity.Current is { } activity)
        {
            activity.SetTag("correlation.id", correlationId);
        }

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
