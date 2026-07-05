namespace Healthcare.Presentation.API.Middleware;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var response = context.Response;

        response.Headers.Append("X-Content-Type-Options", "nosniff");
        response.Headers.Append("X-Frame-Options", "DENY");
        response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

        if (context.Request.IsHttps)
        {
            response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
        }

        response.Headers.Append("Content-Security-Policy", "default-src 'self'");

        await _next(context);
    }
}
