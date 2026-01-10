using FluentAssertions;
using Healthcare.Domain.Common;
using Healthcare.Domain.ValueObjects;
using Xunit;

namespace Healthcare.UnitTests.Domain.ValueObjects;

/// <summary>
/// Unit tests for AppointmentTime value object.
/// </summary>
/// <remarks>
/// Business Rules Tested:
/// 1. Must be in the future
/// 2. Must be during working hours (8 AM - 6 PM)
/// 3. Must be on 30-minute intervals (:00 or :30)
/// 4. Cannot be on weekends
/// 5. Must be at least 1 hour in advance
/// </remarks>
public class AppointmentTimeTests
{
    #region Valid AppointmentTime Tests

    [Fact]
    public void Create_WithValidFutureTime_ShouldSucceed()
    {
        // Arrange - Monday at 10:00 AM, 3 days from now
        var futureTime = GetNextWeekday(DayOfWeek.Monday)
            .Date.AddHours(10);

        // Act
        var appointmentTime = AppointmentTime.Create(futureTime);

        // Assert
        appointmentTime.Should().NotBeNull();
        appointmentTime.Value.Should().BeCloseTo(futureTime.ToUniversalTime(), TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(8, 0)]   // 8:00 AM
    [InlineData(8, 30)]  // 8:30 AM
    [InlineData(12, 0)]  // 12:00 PM
    [InlineData(17, 30)] // 5:30 PM
    public void Create_WithinWorkingHours_ShouldSucceed(int hour, int minute)
    {
        // Arrange - Next Monday at specified time
        var futureTime = GetNextWeekday(DayOfWeek.Monday)
            .Date.AddHours(hour).AddMinutes(minute);

        // Act
        var appointmentTime = AppointmentTime.Create(futureTime);

        // Assert
        appointmentTime.Should().NotBeNull();
    }

    #endregion

    #region Business Rule Violation Tests

    [Fact]
    public void Create_WithPastTime_ShouldThrowInvalidAppointmentTimeException()
    {
        // Arrange
        var pastTime = DateTime.Now.AddDays(-1);

        // Act
        Action act = () => AppointmentTime.Create(pastTime);

        // Assert
        act.Should().Throw<InvalidAppointmentTimeException>()
            .WithMessage("*must be in the future*");
    }

    [Fact]
    public void Create_WithCurrentTime_ShouldThrowInvalidAppointmentTimeException()
    {
        // Arrange
        var now = DateTime.Now;

        // Act
        Action act = () => AppointmentTime.Create(now);

        // Assert
        act.Should().Throw<InvalidAppointmentTimeException>()
            .WithMessage("*must be in the future*");
    }

    [Fact]
    public void Create_LessThanOneHourInAdvance_ShouldThrowInvalidAppointmentTimeException()
    {
        // Arrange - 30 minutes from now
        var tooSoon = DateTime.Now.AddMinutes(30);

        // Act
        Action act = () => AppointmentTime.Create(tooSoon);

        // Assert
        act.Should().Throw<InvalidAppointmentTimeException>()
            .WithMessage("*at least 1 hour*");
    }

    [Theory]
    [InlineData(7, 0)]   // 7:00 AM - Too early
    [InlineData(7, 30)]  // 7:30 AM - Too early
    [InlineData(18, 0)]  // 6:00 PM - Too late
    [InlineData(18, 30)] // 6:30 PM - Too late
    [InlineData(20, 0)]  // 8:00 PM - Too late
    public void Create_OutsideWorkingHours_ShouldThrowInvalidAppointmentTimeException(int hour, int minute)
    {
        // Arrange
        var outsideHours = GetNextWeekday(DayOfWeek.Monday)
            .Date.AddHours(hour).AddMinutes(minute);

        // Act
        Action act = () => AppointmentTime.Create(outsideHours);

        // Assert
        act.Should().Throw<InvalidAppointmentTimeException>()
            .WithMessage("*between 8:00 and 18:00*");
    }

    [Theory]
    [InlineData(DayOfWeek.Saturday)]
    [InlineData(DayOfWeek.Sunday)]
    public void Create_OnWeekend_ShouldThrowInvalidAppointmentTimeException(DayOfWeek weekendDay)
    {
        // Arrange
        var weekend = GetNextWeekday(weekendDay)
            .Date.AddHours(10);

        // Act
        Action act = () => AppointmentTime.Create(weekend);

        // Assert
        act.Should().Throw<InvalidAppointmentTimeException>()
            .WithMessage("*cannot be scheduled on weekends*");
    }

    [Theory]
    [InlineData(10, 15)] // 10:15 AM
    [InlineData(10, 45)] // 10:45 AM
    [InlineData(14, 20)] // 2:20 PM
    public void Create_NotOn30MinuteInterval_ShouldThrowInvalidAppointmentTimeException(int hour, int minute)
    {
        // Arrange
        var invalidTime = GetNextWeekday(DayOfWeek.Monday)
            .Date.AddHours(hour).AddMinutes(minute);

        // Act
        Action act = () => AppointmentTime.Create(invalidTime);

        // Assert
        act.Should().Throw<InvalidAppointmentTimeException>()
            .WithMessage("*:00 or half-hour :30*");
    }

    [Fact]
    public void Create_WithSecondsOrMilliseconds_ShouldThrowInvalidAppointmentTimeException()
    {
        // Arrange
        var timeWithSeconds = GetNextWeekday(DayOfWeek.Monday)
            .Date.AddHours(10).AddMinutes(30).AddSeconds(15);

        // Act
        Action act = () => AppointmentTime.Create(timeWithSeconds);

        // Assert
        act.Should().Throw<InvalidAppointmentTimeException>()
            .WithMessage("*must not include seconds*");
    }

    #endregion

    #region Helper Methods Tests

    [Fact]
    public void GetDate_ShouldReturnDateOnly()
    {
        // Arrange
        var futureTime = GetNextWeekday(DayOfWeek.Monday)
            .Date.AddHours(10);
        var appointmentTime = AppointmentTime.Create(futureTime);

        // Act
        var date = appointmentTime.GetDate();

        // Assert
        date.Should().Be(DateOnly.FromDateTime(futureTime));
    }

    [Fact]
    public void GetTime_ShouldReturnTimeOnly()
    {
        // Arrange
        var futureTime = GetNextWeekday(DayOfWeek.Monday)
            .Date.AddHours(14).AddMinutes(30);
        var appointmentTime = AppointmentTime.Create(futureTime);

        // Act
        var time = appointmentTime.GetTime();

        // Assert
        time.Hour.Should().Be(14);
        time.Minute.Should().Be(30);
    }

    [Fact]
    public void IsWithinNext24Hours_WithAppointmentIn23Hours_ShouldReturnTrue()
    {
        // Arrange
        var futureTime = DateTime.Now.AddHours(23).Date
            .AddHours(DateTime.Now.Hour + 23).AddMinutes(30);

        // Make sure it's on a weekday
        while (futureTime.DayOfWeek == DayOfWeek.Saturday || futureTime.DayOfWeek == DayOfWeek.Sunday)
        {
            futureTime = futureTime.AddDays(1);
        }

        // Make sure it's within working hours
        if (futureTime.Hour < 8)
        {
            futureTime = futureTime.Date.AddHours(10);
        }
        else if (futureTime.Hour >= 18)
        {
            futureTime = futureTime.Date.AddDays(1).AddHours(10);
        }

        var appointmentTime = AppointmentTime.Create(futureTime);

        // Act
        var result = appointmentTime.IsWithinNext24Hours();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsWithinNext24Hours_WithAppointmentIn25Hours_ShouldReturnFalse()
    {
        // Arrange - 2 days from now at 10 AM
        var futureTime = GetNextWeekday(DayOfWeek.Monday)
            .AddDays(2).Date.AddHours(10);

        var appointmentTime = AppointmentTime.Create(futureTime);

        // Act
        var result = appointmentTime.IsWithinNext24Hours();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsPast_WithFutureAppointment_ShouldReturnFalse()
    {
        // Arrange
        var futureTime = GetNextWeekday(DayOfWeek.Monday)
            .Date.AddHours(10);
        var appointmentTime = AppointmentTime.Create(futureTime);

        // Act
        var result = appointmentTime.IsPast();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Display Tests

    [Fact]
    public void ToDisplayString_ShouldReturnFormattedString()
    {
        // Arrange
        var futureTime = GetNextWeekday(DayOfWeek.Monday)
            .Date.AddHours(14).AddMinutes(30);
        var appointmentTime = AppointmentTime.Create(futureTime);

        // Act
        var display = appointmentTime.ToDisplayString();

        // Assert
        display.Should().Contain("Monday");
        display.Should().Contain("2:30 PM");
    }

    [Fact]
    public void ToString_ShouldReturnISO8601Format()
    {
        // Arrange
        var futureTime = new DateTime(2026, 2, 10, 10, 0, 0, DateTimeKind.Utc);
        var appointmentTime = AppointmentTime.Create(futureTime);

        // Act
        var result = appointmentTime.ToString();

        // Assert
        result.Should().Contain("2026-02-10");
        result.Should().EndWith("Z"); // UTC indicator
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameTime_ShouldReturnTrue()
    {
        // Arrange
        var futureTime = GetNextWeekday(DayOfWeek.Monday)
            .Date.AddHours(10);

        var time1 = AppointmentTime.Create(futureTime);
        var time2 = AppointmentTime.Create(futureTime);

        // Act & Assert
        time1.Should().Be(time2);
        (time1 == time2).Should().BeTrue();
        time1.GetHashCode().Should().Be(time2.GetHashCode());
    }

    [Fact]
    public void Equals_WithDifferentTime_ShouldReturnFalse()
    {
        // Arrange
        var monday = GetNextWeekday(DayOfWeek.Monday);
        var time1 = AppointmentTime.Create(monday.Date.AddHours(10));
        var time2 = AppointmentTime.Create(monday.Date.AddHours(14));

        // Act & Assert
        time1.Should().NotBe(time2);
        (time1 != time2).Should().BeTrue();
    }

    #endregion

    #region Test Helper Methods

    /// <summary>
    /// Gets the next occurrence of a specific weekday from now.
    /// </summary>
    private static DateTime GetNextWeekday(DayOfWeek targetDay)
    {
        var today = DateTime.Now.Date;
        var daysUntilTarget = ((int)targetDay - (int)today.DayOfWeek + 7) % 7;

        // If today is the target day, get next week's occurrence
        if (daysUntilTarget == 0)
        {
            daysUntilTarget = 7;
        }

        return today.AddDays(daysUntilTarget);
    }

    #endregion
}