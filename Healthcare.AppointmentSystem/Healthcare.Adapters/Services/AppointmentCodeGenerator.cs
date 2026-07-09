using Healthcare.Domain.Services;

namespace Healthcare.Adapters.Services;

/// <summary>
/// Thread-safe Singleton that generates unique appointment reference codes.
/// </summary>
public sealed class AppointmentCodeGenerator : IAppointmentCodeGenerator
{
    private static volatile AppointmentCodeGenerator? _instance;
    private static readonly object _lock = new object();

    public static AppointmentCodeGenerator Instance
    {
        get
        {
            if (_instance is null)
            {
                lock (_lock)
                {
                    if (_instance is null)
                    {
                        _instance = new AppointmentCodeGenerator();
                    }
                }
            }
            return _instance;
        }
    }

    private AppointmentCodeGenerator()
    {
        _counter = 0;
        _createdAt = DateTime.UtcNow;
    }

    private int _counter;
    private readonly DateTime _createdAt;

    public int TotalGenerated => _counter;

    public string GenerateCode()
    {
        var sequence = System.Threading.Interlocked.Increment(ref _counter);
        var today = DateTime.UtcNow;
        return $"APT-{today:yyyyMMdd}-{sequence:D4}";
    }

    public override string ToString()
    {
        return $"AppointmentCodeGenerator [Instance HashCode: {GetHashCode()}, " +
               $"Created: {_createdAt:HH:mm:ss}, " +
               $"Total Generated: {_counter}]";
    }
}
