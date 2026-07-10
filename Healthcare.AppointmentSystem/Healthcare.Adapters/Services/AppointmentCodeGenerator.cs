using Healthcare.Domain.Services;

namespace Healthcare.Adapters.Services;

/// <summary>
/// In-process, thread-safe implementation of <see cref="IAppointmentCodeGenerator"/>.
/// Suitable for single-instance deployments and tests.
/// For multi-instance production, use <see cref="RedisAppointmentCodeGenerator"/>.
/// </summary>
/// <remarks>
/// Registered via DI (typically as a Singleton lifetime). This class is intentionally
/// NOT a hand-rolled Singleton — it has a public constructor and no static Instance
/// so it can be replaced, mocked, and scoped by the container.
/// </remarks>
public sealed class AppointmentCodeGenerator : IAppointmentCodeGenerator
{
    private int _counter;
    private readonly DateTime _createdAt = DateTime.UtcNow;

    public int TotalGenerated => Volatile.Read(ref _counter);

    /// <summary>
    /// Generates a unique appointment reference code within this process.
    /// Format: APT-YYYYMMDD-XXXX (e.g., APT-20260226-0001)
    /// </summary>
    public string GenerateCode()
    {
        var sequence = Interlocked.Increment(ref _counter);
        var today = DateTime.UtcNow;
        return $"APT-{today:yyyyMMdd}-{sequence:D4}";
    }

    public override string ToString()
    {
        return $"AppointmentCodeGenerator [HashCode: {GetHashCode()}, " +
               $"Created: {_createdAt:HH:mm:ss}, " +
               $"Total Generated: {TotalGenerated}]";
    }
}
