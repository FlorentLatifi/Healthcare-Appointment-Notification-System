using FluentAssertions;
using Healthcare.Presentation.API.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Healthcare.UnitTests.Presentation;

/// <summary>
/// Pure unit tests for <see cref="ProductionStartupGuards.EnsureTrustedProxyConfigOrThrow"/>
/// (no full host spin-up). Complements <see cref="TrustedProxyStartupTests"/> host checks.
/// </summary>
public sealed class TrustedProxyGuardTests
{
    [Fact]
    public void Production_WithoutProxiesOrNetworks_Throws()
    {
        var env = new FakeHostEnvironment(Environments.Production);
        var config = BuildConfig(proxies: null, networks: null);

        var act = () => ProductionStartupGuards.EnsureTrustedProxyConfigOrThrow(env, config);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("TrustedProxies")
            .And.Contain("TrustedNetworks");
    }

    [Fact]
    public void Production_WithOnlyWhitespaceEntries_Throws()
    {
        var env = new FakeHostEnvironment(Environments.Production);
        var config = BuildConfig(proxies: new[] { "  ", "" }, networks: new[] { "\t" });

        var act = () => ProductionStartupGuards.EnsureTrustedProxyConfigOrThrow(env, config);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("TrustedProxies");
    }

    [Fact]
    public void Production_WithTrustedProxies_ReturnsTrue()
    {
        var env = new FakeHostEnvironment(Environments.Production);
        var config = BuildConfig(proxies: new[] { "10.0.0.1" }, networks: null);

        ProductionStartupGuards.EnsureTrustedProxyConfigOrThrow(env, config)
            .Should().BeTrue();
    }

    [Fact]
    public void Production_WithTrustedNetworksOnly_ReturnsTrue()
    {
        var env = new FakeHostEnvironment(Environments.Production);
        var config = BuildConfig(proxies: null, networks: new[] { "10.0.0.0/8" });

        ProductionStartupGuards.EnsureTrustedProxyConfigOrThrow(env, config)
            .Should().BeTrue();
    }

    [Fact]
    public void Development_WithoutConfig_ReturnsFalse_DoesNotThrow()
    {
        var env = new FakeHostEnvironment(Environments.Development);
        var config = BuildConfig(proxies: null, networks: null);

        var act = () => ProductionStartupGuards.EnsureTrustedProxyConfigOrThrow(env, config);

        act.Should().NotThrow();
        act().Should().BeFalse();
    }

    [Fact]
    public void Staging_WithoutConfig_ReturnsFalse_DoesNotThrow()
    {
        var env = new FakeHostEnvironment("Staging");
        var config = BuildConfig(proxies: null, networks: null);

        ProductionStartupGuards.EnsureTrustedProxyConfigOrThrow(env, config)
            .Should().BeFalse();
    }

    private static IConfiguration BuildConfig(string[]? proxies, string[]? networks)
    {
        var values = new Dictionary<string, string?>();
        if (proxies is not null)
        {
            for (var i = 0; i < proxies.Length; i++)
                values[$"TrustedProxies:{i}"] = proxies[i];
        }

        if (networks is not null)
        {
            for (var i = 0; i < networks.Length; i++)
                values[$"TrustedNetworks:{i}"] = networks[i];
        }

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
