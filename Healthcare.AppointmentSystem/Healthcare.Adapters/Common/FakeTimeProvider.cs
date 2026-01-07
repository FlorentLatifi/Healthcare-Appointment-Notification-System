using Healthcare.Application.Ports.Common;

namespace Healthcare.Adapters.Common;

/// <summary>
/// Fake implementation of ITimeProvider for testing.
/// </summary>
/// <remarks>
/// Design Pattern: Test Double (Fake) + Adapter Pattern
/// 
/// This allows complete control over time in unit tests.
/// 
/// Example Test:
/// ```csharp
/// var fakeTime = new FakeTimeProvider();
/// fakeTime.SetUtcNow(new DateTime(2025, 1, 15, 10, 0, 0));
/// 
/// // Now all code using ITimeProvider sees January 15, 2025 at 10:00
/// var appointment = AppointmentTime.Create(fakeTime.UtcNow.AddHours(25));
/// Assert.True(appointment.IsWithinNext24Hours());
/// 
/// // Advance time 23 hours
/// fakeTime.AdvanceHours(23);
/// 
/// // Still within 24 hours
/// Assert.True(appointment.IsWithinNext24Hours());
/// 
/// // Advance 2 more hours
/// fakeTime.AdvanceHours(2);
/// 
/// // Now outside 24-hour window
/// Assert.False(appointment.IsWithinNext24Hours());
/// ```
/// 
/// Benefits:
/// - Deterministic tests (no flaky time-based tests)
/// - Test time-sensitive business rules easily
/// - Simulate past/future scenarios
/// - "Time travel" in tests
/// </remarks>
public sealed class FakeTimeProvider : ITimeProvider
{
    private DateTime _utcNow;
    private DateTime _now;
    private DateTime _today;

    /// <summary>
    /// Initializes with current system time.
    /// </summary>
    public FakeTimeProvider()
    {
        var systemTime = DateTime.UtcNow;
        _utcNow = systemTime;
        _now = systemTime.ToLocalTime();
        _today = _now.Date;
    }

    /// <summary>
    /// Initializes with a specific time.
    /// </summary>
    public FakeTimeProvider(DateTime utcNow)
    {
        SetUtcNow(utcNow);
    }

    public DateTime UtcNow => _utcNow;
    public DateTime Now => _now;
    public DateTime Today => _today;

    /// <summary>
    /// Sets the fake UTC time.
    /// </summary>
    public void SetUtcNow(DateTime utcNow)
    {
        _utcNow = utcNow.Kind == DateTimeKind.Utc
            ? utcNow
            : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);

        _now = _utcNow.ToLocalTime();
        _today = _now.Date;
    }

    /// <summary>
    /// Sets the fake local time.
    /// </summary>
    public void SetNow(DateTime now)
    {
        _now = now.Kind == DateTimeKind.Local
            ? now
            : DateTime.SpecifyKind(now, DateTimeKind.Local);

        _utcNow = _now.ToUniversalTime();
        _today = _now.Date;
    }

    /// <summary>
    /// Sets the fake today's date.
    /// </summary>
    public void SetToday(DateTime today)
    {
        _today = today.Date;
        _now = _today;
        _utcNow = _today.ToUniversalTime();
    }

    /// <summary>
    /// Advances time by a specific duration.
    /// </summary>
    /// <remarks>
    /// Useful for testing scenarios like:
    /// - "24 hours later, appointment needs reminder"
    /// - "1 week later, appointment expires"
    /// - "30 minutes later, doctor availability changes"
    /// </remarks>
    public void AdvanceTime(TimeSpan duration)
    {
        SetUtcNow(_utcNow.Add(duration));
    }

    /// <summary>
    /// Advances time by a number of days.
    /// </summary>
    public void AdvanceDays(int days)
    {
        AdvanceTime(TimeSpan.FromDays(days));
    }

    /// <summary>
    /// Advances time by a number of hours.
    /// </summary>
    public void AdvanceHours(int hours)
    {
        AdvanceTime(TimeSpan.FromHours(hours));
    }

    /// <summary>
    /// Advances time by a number of minutes.
    /// </summary>
    public void AdvanceMinutes(int minutes)
    {
        AdvanceTime(TimeSpan.FromMinutes(minutes));
    }

    /// <summary>
    /// Rewinds time by a specific duration (time travel to the past).
    /// </summary>
    public void RewindTime(TimeSpan duration)
    {
        SetUtcNow(_utcNow.Subtract(duration));
    }

    /// <summary>
    /// Resets to current system time.
    /// </summary>
    public void Reset()
    {
        SetUtcNow(DateTime.UtcNow);
    }

    /// <summary>
    /// Freezes time at the current value.
    /// Subsequent calls to UtcNow, Now, Today return the same value.
    /// </summary>
    /// <remarks>
    /// Already frozen by default - time doesn't advance automatically.
    /// This method is here for clarity in tests.
    /// </remarks>
    public void Freeze()
    {
        // Already frozen - FakeTimeProvider doesn't auto-advance
        // This is just for semantic clarity in tests
    }
}