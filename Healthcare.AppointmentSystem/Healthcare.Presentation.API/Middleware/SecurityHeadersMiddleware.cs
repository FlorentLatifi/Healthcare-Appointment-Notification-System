namespace Healthcare.Presentation.API.Middleware;

/// <summary>
/// Defense-in-depth HTTP security headers for a JSON API.
/// Headers are applied before the rest of the pipeline so they appear on all
/// responses (including error responses handled further down).
/// </summary>
/// <remarks>
/// Production / non-Development: tight CSP; HSTS when the request is HTTPS
/// (ASP.NET <c>UseHsts()</c> is also registered outside Development).
/// Development: looser CSP so Swagger UI can load.
/// Does not affect rate limiting, CORS, or authentication order downstream.
/// </remarks>
public sealed class SecurityHeadersMiddleware
{
    /// <summary>Strict CSP for API JSON responses (no scripts/styles/frames).</summary>
    public const string ProductionCsp =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'; img-src 'none'; script-src 'none'; style-src 'none'; connect-src 'self'; object-src 'none'";

    /// <summary>Looser CSP for Development Swagger UI.</summary>
    public const string DevelopmentCsp =
        "default-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; script-src 'self' 'unsafe-inline'; connect-src 'self'; frame-ancestors 'none'";

    public const string PermissionsPolicyValue =
        "accelerometer=(), camera=(), display-capture=(), geolocation=(), gyroscope=(), " +
        "magnetometer=(), microphone=(), payment=(), publickey-credentials-get=(), usb=()";

    public const string HstsValue = "max-age=31536000; includeSubDomains; preload";

    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;

    public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Apply immediately so headers are present even if a later component
        // short-circuits or writes the body without going through OnStarting in tests.
        // Also register OnStarting for cases where headers were cleared before the response starts.
        ApplyHeaders(context);

        context.Response.OnStarting(static state =>
        {
            ApplyHeadersStatic((HttpContext)state!);
            return Task.CompletedTask;
        }, context);

        await _next(context);
    }

    private void ApplyHeaders(HttpContext context) =>
        ApplyHeadersCore(context, _environment.IsDevelopment());

    private static void ApplyHeadersStatic(HttpContext context)
    {
        // Re-apply only missing headers at response start (e.g. after exception rewrites).
        var env = context.RequestServices.GetService(typeof(IHostEnvironment)) as IHostEnvironment;
        var isDev = env?.IsDevelopment() ?? false;
        ApplyHeadersCore(context, isDev);
    }

    private static void ApplyHeadersCore(HttpContext context, bool isDev)
    {
        var headers = context.Response.Headers;

        SetIfAbsent(headers, "X-Content-Type-Options", "nosniff");
        SetIfAbsent(headers, "X-Frame-Options", "DENY");
        SetIfAbsent(headers, "Referrer-Policy", "no-referrer");
        SetIfAbsent(headers, "Permissions-Policy", PermissionsPolicyValue);
        SetIfAbsent(headers, "Cross-Origin-Resource-Policy", "same-site");
        SetIfAbsent(headers, "Cross-Origin-Opener-Policy", "same-origin");
        SetIfAbsent(headers, "X-Permitted-Cross-Domain-Policies", "none");

        // PHI / auth responses must not sit in shared caches.
        if (!headers.ContainsKey("Cache-Control"))
        {
            headers["Cache-Control"] = "no-store, no-cache, must-revalidate, private";
            if (!headers.ContainsKey("Pragma"))
                headers["Pragma"] = "no-cache";
        }

        SetIfAbsent(
            headers,
            "Content-Security-Policy",
            isDev ? DevelopmentCsp : ProductionCsp);

        // HSTS: also emitted by ASP.NET UseHsts() outside Development.
        if (!isDev && context.Request.IsHttps)
            SetIfAbsent(headers, "Strict-Transport-Security", HstsValue);
    }

    private static void SetIfAbsent(IHeaderDictionary headers, string name, string value)
    {
        if (!headers.ContainsKey(name))
            headers[name] = value;
    }
}
