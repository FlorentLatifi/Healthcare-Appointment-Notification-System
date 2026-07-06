using Healthcare.Domain.Services;
using StackExchange.Redis;

namespace Healthcare.Adapters.Services;

/// <summary>
/// Redis-based implementation of IAppointmentCodeGenerator for multi-instance safety.
/// Uses INCR on a Redis key scoped by date to ensure unique, collision-free codes
/// across multiple API instances behind a load balancer.
/// </summary>
/// <remarks>
/// Design:
/// - Key format: "appt-code-counter:{yyyyMMdd}"
/// - Code format: "APT-{yyyyMMdd}-{XXXX}" (4-digit zero-padded sequence)
/// - Expiry: 48 hours (ensures counter key doesn't grow forever)
/// - Thread-safe: Redis INCR is atomic at the server level
/// - Multi-instance safe: Works across N API instances
///
/// Performance:
/// - Single Redis call (INCR) per code generation
/// - Atomic operation at Redis server (no race conditions)
/// - Sequence resets daily (new key for each day)
///
/// Failure Handling:
/// - If Redis is unavailable, GenerateCode throws RedisConnectionException
/// - Caller (BookAppointmentHandler) should handle via retry logic
/// </remarks>
public sealed class RedisAppointmentCodeGenerator : IAppointmentCodeGenerator
{
    private readonly IConnectionMultiplexer _redis;
    private readonly int _sequenceMax;

    public RedisAppointmentCodeGenerator(
        IConnectionMultiplexer redis,
        int sequenceMax = 9999)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _sequenceMax = sequenceMax;
    }

    /// <summary>
    /// Generates a unique appointment reference code using Redis INCR.
    /// Format: APT-{yyyyMMdd}-{XXXX}
    /// Example: APT-20260226-0001
    /// </summary>
    /// <exception cref="RedisConnectionException">Thrown if Redis is unavailable.</exception>
    public string GenerateCode()
    {
        try
        {
            var db = _redis.GetDatabase();
            var today = DateTime.UtcNow;
            var dateString = today.ToString("yyyyMMdd");
            var counterKey = $"appt-code-counter:{dateString}";
            
            // Atomic increment at Redis server level
            var sequence = db.StringIncrement(counterKey);
            
            // Set expiry on first creation (48 hours ensures cleanup)
            if (sequence == 1)
            {
                db.KeyExpire(counterKey, TimeSpan.FromHours(48));
            }
            
            // Format: APT-20260226-0001
            return $"APT-{dateString}-{sequence:D4}";
        }
        catch (RedisConnectionException ex)
        {
            throw new InvalidOperationException(
                "Redis unavailable for appointment code generation. " +
                "Ensure Redis is running and accessible.", ex);
        }
    }

    /// <summary>
    /// Returns a diagnostic message about Redis connectivity and counter state.
    /// For Redis-based generator, this is informational only (counter is server-side).
    /// </summary>
    public int TotalGenerated
    {
        get
        {
            try
            {
                var db = _redis.GetDatabase();
                var today = DateTime.UtcNow;
                var dateString = today.ToString("yyyyMMdd");
                var counterKey = $"appt-code-counter:{dateString}";
                
                var value = db.StringGet(counterKey);
                return value.IsNull ? 0 : (int)value;
            }
            catch
            {
                return -1; // Indicate unavailable
            }
        }
    }
}
