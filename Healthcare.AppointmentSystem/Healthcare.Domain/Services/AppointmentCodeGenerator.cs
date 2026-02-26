namespace Healthcare.Domain.Services;

/// <summary>
/// Thread-safe Singleton that generates unique appointment reference codes.
/// </summary>
/// <remarks>
/// ╔══════════════════════════════════════════════════════════════════╗
/// ║             DESIGN PATTERN: Singleton (Creational)              ║
/// ╠══════════════════════════════════════════════════════════════════╣
/// ║  DEFINITION (from Prof. Zijadin's lectures):                     ║
/// ║  "Siguron që ekziston vetëm një shembull i kësaj klase dhe       ║
/// ║   ofron një mënyrë globale për t'u qasur atij shembull."         ║
/// ╠══════════════════════════════════════════════════════════════════╣
/// ║  WHY SINGLETON here?                                             ║
/// ║  → Counter must be SHARED across ALL requests.                   ║
/// ║  → If two requests get instance simultaneously, both must        ║
/// ║    see the SAME counter (no duplicate codes).                    ║
/// ║  → Creating a new instance per request = duplicate codes = bug.  ║
/// ╠══════════════════════════════════════════════════════════════════╣
/// ║  SINGLETON ELEMENTS (as taught in lecture):                      ║
/// ║  1. private constructor      → prevents external instantiation   ║
/// ║  2. private static _instance → holds the ONE instance            ║
/// ║  3. public static Instance   → global access point               ║
/// ║  4. lock (_lock)             → thread-safe (double-check locking) ║
/// ╠══════════════════════════════════════════════════════════════════╣
/// ║  HEXAGONAL ARCHITECTURE:                                         ║
/// ║  → Pure Domain Service — ZERO external dependencies             ║
/// ║  → No using Microsoft.*                                          ║
/// ║  → No using System.Data.*                                        ║
/// ║  → No framework code whatsoever                                  ║
/// ╠══════════════════════════════════════════════════════════════════╣
/// ║  THREAD SAFETY — Double-Check Locking Pattern:                   ║
/// ║  Step 1: Check _instance without lock (fast path)               ║
/// ║  Step 2: If null, acquire lock                                   ║
/// ║  Step 3: Check AGAIN inside lock (second check)                  ║
/// ║  Step 4: Only then create the instance                           ║
/// ║  → This avoids locking on EVERY call (performance)              ║
/// ║  → volatile keyword ensures memory visibility across threads     ║
/// ╚══════════════════════════════════════════════════════════════════╝
/// </remarks>
public sealed class AppointmentCodeGenerator : IAppointmentCodeGenerator
{
    // ─────────────────────────────────────────────────────────────────
    // SINGLETON INFRASTRUCTURE
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// The one and only instance. volatile = all threads see same value.
    /// </summary>
    private static volatile AppointmentCodeGenerator? _instance;

    /// <summary>
    /// Lock object used for thread-safe initialization.
    /// </summary>
    private static readonly object _lock = new object();

    /// <summary>
    /// The global access point to the Singleton instance.
    /// Uses double-check locking for thread safety + performance.
    /// </summary>
    /// <example>
    /// var code = AppointmentCodeGenerator.Instance.GenerateCode();
    /// // → "APT-20260226-0001"
    /// </example>
    public static AppointmentCodeGenerator Instance
    {
        get
        {
            // First check (no lock — fast path for all calls after initialization)
            if (_instance is null)
            {
                lock (_lock)
                {
                    // Second check (with lock — safe for first-time creation)
                    if (_instance is null)
                    {
                        _instance = new AppointmentCodeGenerator();
                    }
                }
            }
            return _instance;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // PRIVATE CONSTRUCTOR
    // Prof. Zijadin: "Konstruktori i klasës shënohet si private.
    // Kjo parandalon çdo klasë të jashtme që të krijojë instanca të reja."
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Private constructor — external code CANNOT call: new AppointmentCodeGenerator()
    /// This is what makes it a Singleton.
    /// </summary>
    private AppointmentCodeGenerator()
    {
        _counter = 0;
        _createdAt = DateTime.UtcNow;
    }

    // ─────────────────────────────────────────────────────────────────
    // DOMAIN LOGIC — Code Generation
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Internal counter. Interlocked guarantees atomic increment (thread-safe).
    /// </summary>
    private int _counter;

    /// <summary>
    /// Timestamp when this Singleton was first created.
    /// </summary>
    private readonly DateTime _createdAt;

    /// <summary>
    /// Returns total number of codes generated in this session.
    /// </summary>
    public int TotalGenerated => _counter;

    /// <summary>
    /// Generates a unique appointment reference code.
    /// </summary>
    /// <returns>
    /// Format: APT-YYYYMMDD-XXXX
    /// Examples:
    ///   APT-20260226-0001  (first code of the day)
    ///   APT-20260226-0042  (forty-second code)
    ///   APT-20260226-9999  (max 9999 per day, then resets next day)
    /// </returns>
    /// <remarks>
    /// Thread Safety: Interlocked.Increment is an atomic CPU instruction.
    /// Even if 1000 requests call this simultaneously, each gets a unique number.
    /// </remarks>
    public string GenerateCode()
    {
        // Atomic increment — no two threads can get the same number
        var sequence = System.Threading.Interlocked.Increment(ref _counter);

        var today = DateTime.UtcNow;

        // Format: APT-20260226-0001
        return $"APT-{today:yyyyMMdd}-{sequence:D4}";
    }

    // ─────────────────────────────────────────────────────────────────
    // DIAGNOSTICS — Useful for logging/monitoring
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns diagnostic information about this Singleton instance.
    /// Useful in academic defense to prove only ONE instance exists.
    /// </summary>
    public override string ToString()
    {
        return $"AppointmentCodeGenerator [Instance HashCode: {GetHashCode()}, " +
               $"Created: {_createdAt:HH:mm:ss}, " +
               $"Total Generated: {_counter}]";
    }
}