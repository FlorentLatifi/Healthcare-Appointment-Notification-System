using Healthcare.Domain.Common;

namespace Healthcare.Domain.ValueObjects;

/// <summary>
/// Represents a valid appointment time with global business rules enforced.
/// Doctor-specific working hours are validated separately by Doctor.IsAvailable.
/// </summary>
/// <remarks>
/// 
/// Global Business Rules (doctor-agnostic):
/// 1. Must be in the future (cannot book appointments in the past)
/// 2. Must be at least 1 hour in advance (no same-hour bookings)
/// 3. Must be on 30-minute intervals (:00 or :30 minutes only)
/// 
/// Doctor-specific rules (working hours, day off) are enforced by Doctor.IsAvailable.
/// </remarks>
public sealed class AppointmentTime : ValueObject
{
    private const int MinimumAdvanceHours = 1; // At least 1 hour advance booking

    /// <summary>
    /// Gets the appointment date and time in UTC.
    /// </summary>
    public DateTime Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentTime"/> class.
    /// </summary>
    private AppointmentTime(DateTime value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new AppointmentTime for booking / reschedule paths.
    /// Enforces future time, minimum advance notice, and 30-minute intervals.
    /// </summary>
    /// <exception cref="InvalidAppointmentTimeException">
    /// Thrown when the time violates any global booking rule.
    /// </exception>
    public static AppointmentTime Create(DateTime dateTime)
    {
        var utcDateTime = NormalizeToUtc(dateTime);
        var localDateTime = utcDateTime.ToLocalTime();

        if (localDateTime <= DateTime.Now)
        {
            throw new InvalidAppointmentTimeException(
                "Appointment time must be in the future.");
        }

        if (localDateTime <= DateTime.Now.AddHours(MinimumAdvanceHours))
        {
            throw new InvalidAppointmentTimeException(
                $"Appointments must be booked at least {MinimumAdvanceHours} hour(s) in advance.");
        }

        EnsureValidClockShape(localDateTime);

        return new AppointmentTime(utcDateTime);
    }

    /// <summary>
    /// Rehydrates an AppointmentTime from persisted storage without re-applying
    /// booking rules (future / advance notice). Use only when loading from the database.
    /// </summary>
    /// <remarks>
    /// Calling <see cref="Create"/> on read would throw for past completed appointments
    /// and for confirmed slots that fall inside the one-hour advance window as wall-clock advances.
    /// </remarks>
    public static AppointmentTime FromPersistence(DateTime dateTime)
    {
        var utcDateTime = NormalizeToUtc(dateTime);
        // Structural sanity only — historical rows must still load.
        var localDateTime = utcDateTime.ToLocalTime();
        if (localDateTime.Second != 0 || localDateTime.Millisecond != 0)
        {
            // Truncate rather than fail: legacy data should not break reads.
            utcDateTime = new DateTime(
                utcDateTime.Year, utcDateTime.Month, utcDateTime.Day,
                utcDateTime.Hour, utcDateTime.Minute, 0, DateTimeKind.Utc);
        }

        return new AppointmentTime(utcDateTime);
    }

    private static DateTime NormalizeToUtc(DateTime dateTime)
    {
        return dateTime.Kind == DateTimeKind.Utc
            ? dateTime
            : dateTime.ToUniversalTime();
    }

    private static void EnsureValidClockShape(DateTime localDateTime)
    {
        if (localDateTime.Minute != 0 && localDateTime.Minute != 30)
        {
            throw new InvalidAppointmentTimeException(
                "Appointments must be scheduled on the hour (:00) or half-hour (:30).");
        }

        if (localDateTime.Second != 0 || localDateTime.Millisecond != 0)
        {
            throw new InvalidAppointmentTimeException(
                "Appointment time must not include seconds or milliseconds.");
        }
    }

    /// <summary>
    /// Gets the date portion of the appointment.
    /// </summary>
    public DateOnly GetDate() => DateOnly.FromDateTime(Value.ToLocalTime());

    /// <summary>
    /// Gets the time portion of the appointment.
    /// </summary>
    public TimeOnly GetTime() => TimeOnly.FromDateTime(Value.ToLocalTime());

    /// <summary>
    /// Checks if the appointment is within the next 24 hours.
    /// Useful for sending reminders.
    /// </summary>
    public bool IsWithinNext24Hours()
    {
        var now = DateTime.UtcNow;
        var twentyFourHoursFromNow = now.AddHours(24);
        return Value > now && Value <= twentyFourHoursFromNow;
    }

    /// <summary>
    /// Checks if the appointment time has passed.
    /// </summary>
    public bool IsPast() => Value < DateTime.UtcNow;

    /// <summary>
    /// Gets a formatted string representation for display.
    /// Example: "Monday, January 15, 2025 at 2:30 PM"
    /// </summary>
    public string ToDisplayString()
    {
        var local = Value.ToLocalTime();
        return local.ToString("dddd, MMMM dd, yyyy 'at' h:mm tt");
    }

    /// <summary>
    /// Returns the appointment time as a string in ISO 8601 format.
    /// </summary>
    public override string ToString() => Value.ToString("yyyy-MM-ddTHH:mm:ssZ");

    /// <summary>
    /// Implicit conversion to DateTime for convenience.
    /// </summary>
    public static implicit operator DateTime(AppointmentTime time) => time.Value;

    /// <summary>
    /// Gets the equality components for value object comparison.
    /// </summary>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}