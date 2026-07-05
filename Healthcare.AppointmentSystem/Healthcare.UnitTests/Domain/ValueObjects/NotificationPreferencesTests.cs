using FluentAssertions;
using Healthcare.Domain.ValueObjects;
using Xunit;

namespace Healthcare.UnitTests.Domain.ValueObjects;

public class NotificationPreferencesTests
{
    [Fact]
    public void Create_WithBothEnabled_ShouldSucceed()
    {
        var prefs = NotificationPreferences.Create(true, true);

        prefs.EmailEnabled.Should().BeTrue();
        prefs.SmsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Create_WithBothDisabled_ShouldSucceed()
    {
        var prefs = NotificationPreferences.Create(false, false);

        prefs.EmailEnabled.Should().BeFalse();
        prefs.SmsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Default_ShouldHaveEmailEnabledAndSmsDisabled()
    {
        var prefs = NotificationPreferences.Default();

        prefs.EmailEnabled.Should().BeTrue();
        prefs.SmsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Equals_SameValues_ShouldBeEqual()
    {
        var a = NotificationPreferences.Create(true, false);
        var b = NotificationPreferences.Create(true, false);

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentValues_ShouldNotBeEqual()
    {
        var a = NotificationPreferences.Create(true, false);
        var b = NotificationPreferences.Create(true, true);

        a.Should().NotBe(b);
    }
}
