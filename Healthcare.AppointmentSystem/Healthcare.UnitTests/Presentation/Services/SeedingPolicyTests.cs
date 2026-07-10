using FluentAssertions;
using Healthcare.Presentation.API.Services;
using Xunit;

namespace Healthcare.UnitTests.Presentation.Services;

public sealed class SeedingPolicyTests
{
    [Fact]
    public void CanSeedDemoData_Production_AlwaysFalse_EvenIfFlagsTrue()
    {
        var options = new SeedingOptions
        {
            SeedDemoData = true,
            AllowDemoDataOutsideDevelopment = true
        };

        SeedingPolicy.CanSeedDemoData("Production", options).Should().BeFalse();
    }

    [Fact]
    public void CanSeedDemoData_Development_WhenFlagTrue()
    {
        var options = new SeedingOptions { SeedDemoData = true };

        SeedingPolicy.CanSeedDemoData("Development", options).Should().BeTrue();
    }

    [Fact]
    public void CanSeedDemoData_Development_WhenFlagFalse()
    {
        var options = new SeedingOptions { SeedDemoData = false };

        SeedingPolicy.CanSeedDemoData("Development", options).Should().BeFalse();
    }

    [Fact]
    public void CanSeedDemoData_Docker_RequiresExplicitOutsideDevOptIn()
    {
        var withoutOptIn = new SeedingOptions
        {
            SeedDemoData = true,
            AllowDemoDataOutsideDevelopment = false
        };
        var withOptIn = new SeedingOptions
        {
            SeedDemoData = true,
            AllowDemoDataOutsideDevelopment = true
        };

        SeedingPolicy.CanSeedDemoData("Docker", withoutOptIn).Should().BeFalse();
        SeedingPolicy.CanSeedDemoData("Docker", withOptIn).Should().BeTrue();
    }

    [Fact]
    public void EnsureStrongPassword_RejectsLegacyDefaultAdmin123()
    {
        var act = () => SeedingPolicy.EnsureStrongPassword("Admin123!");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*known weak*");
    }

    [Fact]
    public void EnsureStrongPassword_RejectsShortPassword()
    {
        var act = () => SeedingPolicy.EnsureStrongPassword("Short1!");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least*");
    }

    [Fact]
    public void EnsureStrongPassword_AcceptsStrongPassword()
    {
        var act = () => SeedingPolicy.EnsureStrongPassword("C0rrect-Horse-Battery!");

        act.Should().NotThrow();
    }

    [Fact]
    public void CanGenerateBootstrapPassword_FalseInProduction()
    {
        SeedingPolicy.CanGenerateBootstrapPassword("Production").Should().BeFalse();
        SeedingPolicy.CanGenerateBootstrapPassword("Development").Should().BeTrue();
        SeedingPolicy.CanGenerateBootstrapPassword("Docker").Should().BeTrue();
    }

    [Fact]
    public void GenerateSecurePassword_MeetsPolicy()
    {
        var password = SeedingPolicy.GenerateSecurePassword();

        password.Length.Should().BeGreaterThanOrEqualTo(SeedingPolicy.MinimumPasswordLength);
        var act = () => SeedingPolicy.EnsureStrongPassword(password);
        act.Should().NotThrow();
    }
}
