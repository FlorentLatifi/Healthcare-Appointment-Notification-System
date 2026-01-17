namespace Healthcare.Adapters.Payments;

/// <summary>
/// Configuration settings for Stripe payment gateway.
/// </summary>
/// <remarks>
/// These settings are loaded from appsettings.json:
/// 
/// {
///   "Stripe": {
///     "SecretKey": "sk_test_...",
///     "PublishableKey": "pk_test_...",
///     "WebhookSecret": "whsec_..."
///   }
/// }
/// 
/// Security:
/// - Never commit real keys to source control
/// - Use Azure Key Vault / AWS Secrets Manager in production
/// - Use test keys for development
/// </remarks>
public sealed class StripeSettings
{
    /// <summary>
    /// Stripe secret key (server-side, never expose to frontend).
    /// </summary>
    /// <example>sk_test_51AbCdEfGhIjKlMnOpQrStUvWxYz</example>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Stripe publishable key (safe to expose to frontend).
    /// </summary>
    /// <example>pk_test_51AbCdEfGhIjKlMnOpQrStUvWxYz</example>
    public string PublishableKey { get; set; } = string.Empty;

    /// <summary>
    /// Webhook secret for verifying Stripe webhook signatures.
    /// </summary>
    /// <example>whsec_AbCdEfGhIjKlMnOpQrStUvWxYz</example>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>
    /// Default currency for payments (ISO 4217 code).
    /// </summary>
    public string DefaultCurrency { get; set; } = "USD";
}