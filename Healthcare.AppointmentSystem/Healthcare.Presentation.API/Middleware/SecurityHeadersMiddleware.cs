namespace Healthcare.Presentation.API.Middleware;

/// <summary>
/// Defense-in-depth HTTP security headers for a JSON API (not a browser app shell).
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;

    public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var response = context.Response;

        response.OnStarting(() =>
        {
            var headers = response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";
            headers["Cross-Origin-Resource-Policy"] = "same-site";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";

            // API responses must not be cached by shared caches (PHI risk).
            if (!headers.ContainsKey("Cache-Control"))
            {
                headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
                headers["Pragma"] = "no-cache";
            }

            // Tight CSP for API JSON endpoints (Swagger UI is Development-only).
            if (!_environment.IsDevelopment())
            {
                headers["Content-Security-Policy"] =
                    "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
            }
            else
            {
                headers["Content-Security-Policy"] =
                    "default-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; script-src 'self' 'unsafe-inline'";
            }

            // HSTS is also set via UseHsts() in Program for non-Development.
            if (context.Request.IsHttps && !_environment.IsDevelopment())
            {
                headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
