using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Healthcare.Presentation.API.Configuration;

/// <summary>
/// Fail-fast checks that must pass before the Production host serves traffic.
/// Extracted for unit testing without spinning a full WebApplicationFactory
/// (Serilog bootstrap freezes across sequential host builds in one process).
/// </summary>
public static class ProductionStartupGuards
{
    /// <summary>
    /// Production requires at least one of TrustedProxies or TrustedNetworks
    /// so forwarded client IPs (rate limit / audit) are trustworthy.
    /// </summary>
    /// <returns>
    /// <c>true</c> if config is present; <c>false</c> if missing and environment is non-Production
    /// (caller may log a warning). Throws when missing in Production.
    /// </returns>
    public static bool EnsureTrustedProxyConfigOrThrow(
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        var proxies = configuration.GetSection("TrustedProxies").Get<string[]>()
            ?? Array.Empty<string>();
        var networks = configuration.GetSection("TrustedNetworks").Get<string[]>()
            ?? Array.Empty<string>();
        var hasTrustedProxyConfig =
            proxies.Any(static p => !string.IsNullOrWhiteSpace(p)) ||
            networks.Any(static n => !string.IsNullOrWhiteSpace(n));

        if (hasTrustedProxyConfig)
            return true;

        if (environment.IsProduction())
        {
            throw new InvalidOperationException(
                "Production requires TrustedProxies and/or TrustedNetworks. " +
                "Without them, rate limiting and IP-based audit logs collapse to a single bucket " +
                "behind a reverse proxy. Set TrustedProxies (IP list) and/or TrustedNetworks (CIDR).");
        }

        return false;
    }

    /// <summary>
    /// Production requires Stripe:WebhookSecret so webhook signature verification can fail closed.
    /// Development and other non-Production environments may omit it (caller may log a warning).
    /// </summary>
    /// <returns>
    /// <c>true</c> if a non-empty secret is configured; <c>false</c> if missing and environment is
    /// non-Production. Throws when missing/whitespace in Production.
    /// </returns>
    public static bool EnsureStripeWebhookSecretOrThrow(
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        var webhookSecret = configuration["Stripe:WebhookSecret"];
        if (!string.IsNullOrWhiteSpace(webhookSecret))
            return true;

        if (environment.IsProduction())
        {
            throw new InvalidOperationException(
                "Production requires Stripe:WebhookSecret. " +
                "Without it, Stripe webhook signature verification cannot fail closed. " +
                "Set Stripe:WebhookSecret from the Stripe Dashboard (Developers → Webhooks → signing secret), " +
                "e.g. environment variable Stripe__WebhookSecret or a secrets file.");
        }

        return false;
    }
}
