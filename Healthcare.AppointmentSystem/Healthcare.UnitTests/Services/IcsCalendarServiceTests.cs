using FluentAssertions;
using Healthcare.Application.Services;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Healthcare.UnitTests.Helpers;
using Xunit;

namespace Healthcare.UnitTests.Services;

public class IcsCalendarServiceTests
{
    private static Appointment CreateTestAppointment()
    {
        var patient = new TestDataBuilder.PatientBuilder().Build();
        var doctor = new TestDataBuilder.DoctorBuilder().Build();
        var future = DateTime.UtcNow.Date.AddDays(1).AddHours(10);
        var scheduledTime = AppointmentTime.Create(future);
        return Appointment.Create(patient, doctor, scheduledTime, "Routine checkup");
    }

    [Fact]
    public void GenerateAppointmentIcs_ShouldStartWithVCalendar()
    {
        var appt = CreateTestAppointment();
        var ics = IcsCalendarService.GenerateAppointmentIcs(appt);

        ics.Should().StartWith("BEGIN:VCALENDAR");
    }

    [Fact]
    public void GenerateAppointmentIcs_ShouldEndWithVCalendar()
    {
        var appt = CreateTestAppointment();
        var ics = IcsCalendarService.GenerateAppointmentIcs(appt);

        ics.TrimEnd().Should().EndWith("END:VCALENDAR");
    }

    [Fact]
    public void GenerateAppointmentIcs_ShouldContainVEVENT()
    {
        var appt = CreateTestAppointment();
        var ics = IcsCalendarService.GenerateAppointmentIcs(appt);

        ics.Should().Contain("BEGIN:VEVENT");
        ics.Should().Contain("END:VEVENT");
    }

    [Fact]
    public void GenerateAppointmentIcs_ShouldContainVERSION()
    {
        var appt = CreateTestAppointment();
        var ics = IcsCalendarService.GenerateAppointmentIcs(appt);

        ics.Should().Contain("VERSION:2.0");
    }

    [Fact]
    public void GenerateAppointmentIcs_ShouldContainUid()
    {
        var appt = CreateTestAppointment();
        var ics = IcsCalendarService.GenerateAppointmentIcs(appt);

        ics.Should().Contain($"UID:appointment-{appt.ReferenceCode}@healthcare");
    }

    [Fact]
    public void GenerateAppointmentIcs_ShouldContainDtStart()
    {
        var appt = CreateTestAppointment();
        var ics = IcsCalendarService.GenerateAppointmentIcs(appt);

        ics.Should().Contain("DTSTART");
    }

    [Fact]
    public void GenerateAppointmentIcs_ShouldContainDtEnd()
    {
        var appt = CreateTestAppointment();
        var ics = IcsCalendarService.GenerateAppointmentIcs(appt);

        ics.Should().Contain("DTEND");
    }

    [Fact]
    public void GenerateAppointmentIcs_ShouldContainSummary()
    {
        var appt = CreateTestAppointment();
        var ics = IcsCalendarService.GenerateAppointmentIcs(appt);

        ics.Should().Contain($"SUMMARY:Appointment with Dr. {appt.Doctor.FullName} - {appt.ReferenceCode}");
    }

    [Fact]
    public void GenerateAppointmentIcs_ShouldContainDescription()
    {
        var appt = CreateTestAppointment();
        var ics = IcsCalendarService.GenerateAppointmentIcs(appt);

        ics.Should().Contain("DESCRIPTION:Routine checkup");
    }

    [Fact]
    public void GenerateAppointmentIcs_ShouldContainLocation()
    {
        var appt = CreateTestAppointment();
        var ics = IcsCalendarService.GenerateAppointmentIcs(appt);

        ics.Should().Contain($"LOCATION:Dr. {appt.Doctor.FullName}");
    }

    [Fact]
    public void GenerateAppointmentIcs_ForPendingAppointment_StatusTentative()
    {
        var appt = CreateTestAppointment();
        var ics = IcsCalendarService.GenerateAppointmentIcs(appt);

        ics.Should().Contain("STATUS:TENTATIVE");
    }

    [Fact]
    public void GenerateAppointmentIcs_ForConfirmedAppointment_StatusConfirmed()
    {
        var appt = CreateTestAppointment();
        appt.Confirm();
        var ics = IcsCalendarService.GenerateAppointmentIcs(appt);

        ics.Should().Contain("STATUS:CONFIRMED");
    }

    [Fact]
    public void GenerateAppointmentIcs_ForCancelledAppointment_StatusCancelled()
    {
        var appt = CreateTestAppointment();
        appt.Confirm();
        appt.Cancel("Patient requested cancellation");
        var ics = IcsCalendarService.GenerateAppointmentIcs(appt);

        ics.Should().Contain("STATUS:CANCELLED");
    }

    [Fact]
    public void GenerateAppointmentIcs_ShouldContainProdId()
    {
        var appt = CreateTestAppointment();
        var ics = IcsCalendarService.GenerateAppointmentIcs(appt);

        ics.Should().Contain("PRODID:-//Healthcare System//Appointments//EN");
    }

    [Fact]
    public void GenerateAppointmentIcs_ShouldContainDtStamp()
    {
        var appt = CreateTestAppointment();
        var ics = IcsCalendarService.GenerateAppointmentIcs(appt);

        ics.Should().Contain("DTSTAMP:");
    }
}
