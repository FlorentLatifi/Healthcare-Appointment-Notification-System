using Healthcare.Domain.Common;

namespace Healthcare.Domain.ValueObjects;

public sealed class DoctorWorkingHours : ValueObject
{
    public DayOfWeek DayOfWeek { get; }
    public TimeOnly? StartTime { get; }
    public TimeOnly? EndTime { get; }
    public bool IsWorkingDay { get; }

    private DoctorWorkingHours(DayOfWeek dayOfWeek, TimeOnly? startTime, TimeOnly? endTime, bool isWorkingDay)
    {
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
        IsWorkingDay = isWorkingDay;
    }

    public static DoctorWorkingHours Create(DayOfWeek dayOfWeek, TimeOnly startTime, TimeOnly endTime)
    {
        if (startTime >= endTime)
            throw new ArgumentException("Start time must be before end time.", nameof(startTime));

        return new DoctorWorkingHours(dayOfWeek, startTime, endTime, true);
    }

    public static DoctorWorkingHours CreateDayOff(DayOfWeek dayOfWeek)
    {
        return new DoctorWorkingHours(dayOfWeek, null, null, false);
    }

    public bool IsWithinHours(TimeOnly time)
    {
        if (!IsWorkingDay || StartTime is null || EndTime is null)
            return false;

        return time >= StartTime.Value && time < EndTime.Value;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return DayOfWeek;
        yield return StartTime;
        yield return EndTime;
        yield return IsWorkingDay;
    }
}
