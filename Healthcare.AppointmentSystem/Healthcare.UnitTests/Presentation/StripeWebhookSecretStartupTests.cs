using FluentAssertions;
using Healthcare.Presentation.API.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Healthcare.UnitTests.Presentation;

/// <summary>
/// Production must fail fast without Stripe:WebhookSecret so webhook signature
/// verification cannot run with an empty signing secret.
/// </summary>
public sealed class StripeWebhookSecretStartupTests
{
    [Fact]
    public void Production_WithoutWebhookSecret_Throws()
    {
        var env = new FakeHostEnvironment(Environments.Production);
        var config = BuildConfig(webhookSecret: null);

        var act = () => ProductionStartupGuards.EnsureStripeWebhookSecretOrThrow(env, config);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("Stripe:WebhookSecret");
    }

    [Fact]
    public void Production_WithWhitespaceWebhookSecret_Throws()
    {
        var env = new FakeHostEnvironment(Environments.Production);
        var config = BuildConfig(webhookSecret: "   ");

        var act = () => ProductionStartupGuards.EnsureStripeWebhookSecretOrThrow(env, config);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("Stripe:WebhookSecret");
    }

    [Fact]
    public void Production_WithWebhookSecret_ReturnsTrue()
    {
        var env = new FakeHostEnvironment(Environments.Production);
        var config = BuildConfig(webhookSecret: "whsec_live_or_test_signing_secret");

        ProductionStartupGuards.EnsureStripeWebhookSecretOrThrow(env, config)
            .Should().BeTrue();
    }

    [Fact]
    public void Development_WithoutWebhookSecret_ReturnsFalse_DoesNotThrow()
    {
        var env = new FakeHostEnvironment(Environments.Development);
        var config = BuildConfig(webhookSecret: null);

        var act = () => ProductionStartupGuards.EnsureStripeWebhookSecretOrThrow(env, config);

        act.Should().NotThrow();
        act().Should().BeFalse();
    }

    [Fact]
    public void Staging_WithoutWebhookSecret_ReturnsFalse_DoesNotThrow()
    {
        // Only Production is hard-fail; non-Production may log a warning from Program.cs.
        var env = new FakeHostEnvironment("Staging");
        var config = BuildConfig(webhookSecret: "");

        ProductionStartupGuards.EnsureStripeWebhookSecretOrThrow(env, config)
            .Should().BeFalse();
    }

    [Fact]
    public void Production_WithEmptyWebhookSecret_Throws()
    {
        var env = new FakeHostEnvironment(Environments.Production);
        var config = BuildConfig(webhookSecret: "");

        var act = () => ProductionStartupGuards.EnsureStripeWebhookSecretOrThrow(env, config);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("Stripe:WebhookSecret");
    }

    private static IConfiguration BuildConfig(string? webhookSecret)
    {
        var values = new Dictionary<string, string?>();
        if (webhookSecret is not null)
            values["Stripe:WebhookSecret"] = webhookSecret;
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Healthcare.Presentation.API";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
