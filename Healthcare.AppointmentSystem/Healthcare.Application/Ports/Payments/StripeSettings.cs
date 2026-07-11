namespace Healthcare.Application.Ports.Payments;

/// <summary>
/// Stripe gateway settings (Application options). Loaded at composition root.
/// </summary>
public sealed class StripeSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string DefaultCurrency { get; set; } = "USD";
}
