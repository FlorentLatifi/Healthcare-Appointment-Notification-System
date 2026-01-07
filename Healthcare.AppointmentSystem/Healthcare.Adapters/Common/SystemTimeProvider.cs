using Healthcare.Application.Ports.Common;

namespace Healthcare.Adapters.Common;

/// <summary>
/// Production implementation of ITimeProvider using system time.
/// </summary>
/// <remarks>
/// Design Pattern: Adapter Pattern + Strategy Pattern
/// 
/// This is the REAL time provider for production.
/// Returns actual DateTime.UtcNow and DateTime.Now.
/// 
/// Usage in DI:
/// services.AddSingleton<ITimeProvider, SystemTimeProvider>();
/// 
/// Why singleton?
/// - Stateless (no internal state)
/// - Thread-safe (DateTime.UtcNow is thread-safe)
/// - Performance (one instance for entire application)
/// </remarks>
public sealed class SystemTimeProvider : ITimeProvider
{
    /// <summary>
    /// Gets the current UTC date and time.
    /// </summary>
    public DateTime UtcNow => DateTime.UtcNow;

    /// <summary>
    /// Gets the current local date and time.
    /// </summary>
    public DateTime Now => DateTime.Now;

    /// <summary>
    /// Gets today's date (local time, no time component).
    /// </summary>
    public DateTime Today => DateTime.Today;
}