namespace Healthcare.Application.Ports.Caching;

/// <summary>
/// Central Redis / cache key naming conventions.
/// Format: <c>{instance}cache:v{version}:{domain}:{...}</c>
/// Locks: <c>{instance}lock:{purpose}:{...}</c>
/// </summary>
/// <remarks>
/// Rules:
/// <list type="bullet">
/// <item>Always use this builder — never hand-build Redis keys in handlers.</item>
/// <item>Include a version segment so schema changes can dual-run.</item>
/// <item>List keys embed a generation counter so invalidation is O(1) (INCR gen).</item>
/// <item>Never put PII/secrets in key names (ids and dates only).</item>
/// </list>
/// </remarks>
public static class CacheKeys
{
    public const int SchemaVersion = 1;

    // ── Generations (invalidate lists by bumping) ─────────────────
    public static string DoctorListGeneration => $"cache:v{SchemaVersion}:doctor:list:gen";

    // ── Doctor catalog ────────────────────────────────────────────
    public static string DoctorById(int doctorId) =>
        $"cache:v{SchemaVersion}:doctor:by-id:{doctorId}";

    public static string DoctorList(string filter, long generation, int page, int pageSize) =>
        $"cache:v{SchemaVersion}:doctor:list:{filter}:g{generation}:p{page}:s{pageSize}";

    public const string DoctorListFilterAll = "all";
    public const string DoctorListFilterActive = "active";
    public const string DoctorListFilterAccepting = "accepting";

    /// <summary>Prefix for SCAN-based purge of all doctor catalog keys (fallback).</summary>
    public static string DoctorCatalogPrefix => $"cache:v{SchemaVersion}:doctor:";

    // ── Schedule (working hours — changes rarely) ─────────────────
    public static string DoctorSchedule(int doctorId) =>
        $"cache:v{SchemaVersion}:schedule:doctor:{doctorId}";

    public static string DoctorSchedulePrefix => $"cache:v{SchemaVersion}:schedule:doctor:";

    // ── Availability (booked slots for a day — changes often) ─────
    public static string DoctorDayAvailability(int doctorId, DateOnly date) =>
        $"cache:v{SchemaVersion}:availability:doctor:{doctorId}:date:{date:yyyyMMdd}";

    public static string DoctorAvailabilityPrefix(int doctorId) =>
        $"cache:v{SchemaVersion}:availability:doctor:{doctorId}:";

    public static string AllAvailabilityPrefix =>
        $"cache:v{SchemaVersion}:availability:";

    // ── Stampede / distributed locks ──────────────────────────────
    public static string StampedeLock(string cacheKey) =>
        $"lock:stampede:{StableHash(cacheKey)}";

    public static string AppointmentBookingLock(int doctorId, DateTime scheduledUtc) =>
        $"lock:appointment:doctor:{doctorId}:time:{scheduledUtc:yyyyMMddHHmm}";

    /// <summary>Short stable hash so lock keys stay short and free of special chars.</summary>
    public static string StableHash(string value)
    {
        // FNV-1a 64-bit
        unchecked
        {
            const ulong offset = 14695981039346656037;
            const ulong prime = 1099511628211;
            ulong hash = offset;
            foreach (var c in value)
            {
                hash ^= c;
                hash *= prime;
            }
            return hash.ToString("x16");
        }
    }
}
