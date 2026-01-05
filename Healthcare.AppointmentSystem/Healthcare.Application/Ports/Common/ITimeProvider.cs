namespace Healthcare.Application.Ports.Common;

/// <summary>
/// Provides access to the current date and time.
/// </summary>
/// <remarks>
/// Design Pattern: Adapter Pattern
/// 
/// This abstracts DateTime.UtcNow to make code testable.
/// 
/// Problem with DateTime.UtcNow:
/// - Unit tests that depend on current time are non-deterministic
/// - Cannot test time-sensitive business rules reliably
/// 
/// Solution with ITimeProvider:
/// - Production: SystemTimeProvider returns real time
/// - Testing: FakeTimeProvider returns controllable time
/// 
/// Example test scenario:
/// "Test that appointments within 24 hours get reminders"
/// - Set fake time to "2025-01-10 10:00"
/// - Create appointment for "2025-01-11 09:00"
/// - Verify reminder is sent
/// </remarks>
public interface ITimeProvider
{
    /// <summary>
    /// Gets the current UTC date and time.
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// Gets the current local date and time.
    /// </summary>
    DateTime Now { get; }

    /// <summary>
    /// Gets today's date (local time, no time component).
    /// </summary>
    DateTime Today { get; }
}