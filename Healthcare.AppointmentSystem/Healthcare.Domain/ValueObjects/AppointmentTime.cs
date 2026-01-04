using Healthcare.Domain.Common;

namespace Healthcare.Domain.ValueObjects;

/// <summary>
/// Represents a valid appointment time with business rules enforced.
/// </summary>
/// <remarks>
/// Design Pattern: Value Object Pattern
/// 
/// Business Rules Enforced:
/// 1. Must be in the future (cannot book appointments in the past)
/// 2. Must be during working hours (8:00 AM - 6:00 PM)
/// 3. Must be on 30-minute intervals (:00 or :30 minutes only)
/// 4. Cannot be on weekends (Saturday/Sunday)
/// 5. Must be at least 1 hour in advance (no same-hour bookings)
/// 
/// This is a "self-validating" value object - invalid times cannot exist.
/// </remarks>
public sealed class AppointmentTime : ValueObject
{
    private const int WorkingHoursStart = 8;  // 8:00 AM
    private const int WorkingHoursEnd = 18;    // 6:00 PM
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
    /// Creates a new AppointmentTime value object with full business rule validation.
    /// </summary>
    /// <param name="dateTime">The desired appointment date and time.</param>
    /// <returns>A valid AppointmentTime value object.</returns>
    /// <exception cref="InvalidAppointmentTimeException">
    /// Thrown when the time violates any business rule.
    /// </exception>
    public static AppointmentTime Create(DateTime dateTime)
    {
        // Convert to UTC for consistent storage
        var utcDateTime = dateTime.Kind == DateTimeKind.Utc
            ? dateTime
            : dateTime.ToUniversalTime();

        // For validation, convert to local time
        var localDateTime = utcDateTime.ToLocalTime();

        // Rule 1: Must be in the future
        if (localDateTime <= DateTime.Now)
        {
            throw new InvalidAppointmentTimeException(
                "Appointment time must be in the future.");
        }

        // Rule 2: Must be at least 1 hour in advance
        if (localDateTime <= DateTime.Now.AddHours(MinimumAdvanceHours))
        {
            throw new InvalidAppointmentTimeException(
                $"Appointments must be booked at least {MinimumAdvanceHours} hour(s) in advance.");
        }

        // Rule 3: Cannot be on weekends
        if (localDateTime.DayOfWeek == DayOfWeek.Saturday ||
            localDateTime.DayOfWeek == DayOfWeek.Sunday)
        {
            throw new InvalidAppointmentTimeException(
                "Appointments cannot be scheduled on weekends.");
        }

        // Rule 4: Must be during working hours
        if (localDateTime.Hour < WorkingHoursStart || localDateTime.Hour >= WorkingHoursEnd)
        {
            throw new InvalidAppointmentTimeException(
                $"Appointments must be scheduled between {WorkingHoursStart}:00 and {WorkingHoursEnd}:00.");
        }

        // Rule 5: Must be on 30-minute intervals
        if (localDateTime.Minute != 0 && localDateTime.Minute != 30)
        {
            throw new InvalidAppointmentTimeException(
                "Appointments must be scheduled on the hour (:00) or half-hour (:30).");
        }

        // Rule 6: Seconds and milliseconds must be zero
        if (localDateTime.Second != 0 || localDateTime.Millisecond != 0)
        {
            throw new InvalidAppointmentTimeException(
                "Appointment time must not include seconds or milliseconds.");
        }

        return new AppointmentTime(utcDateTime);
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