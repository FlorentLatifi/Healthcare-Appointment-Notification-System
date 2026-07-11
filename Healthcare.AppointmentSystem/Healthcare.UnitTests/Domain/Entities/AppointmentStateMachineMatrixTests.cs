using FluentAssertions;
using Healthcare.Adapters.Services;
using Healthcare.Domain.Common;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.Events;
using Healthcare.Domain.ValueObjects;
using Xunit;

namespace Healthcare.UnitTests.Domain.Entities;

/// <summary>
/// Exhaustive Appointment status machine coverage. Illegal transitions cause billing/clinical chaos
/// (e.g. completing a cancelled visit, no-showing before the slot, reopening terminal states).
/// </summary>
public sealed class AppointmentStateMachineMatrixTests
{
    private const string LongReason = "State machine matrix test reason text";
    private const string LongNotes = "Doctor notes long enough for complete transition.";
    private const string CancelReason = "Cancellation reason long enough here";

    private static Patient CreatePatient() => Patient.Create(
        "SM", "Patient", Email.Create("sm.patient@test.com"), PhoneNumber.Create("+38344123456"),
        new DateTime(1988, 3, 3), Gender.Male,
        Address.Create("1 St", "Pristina", "KS", "10000", "Kosovo"));

    private static Doctor CreateDoctor() => Doctor.Create(
        "SM", "Doctor", Email.Create("sm.doctor@test.com"), PhoneNumber.Create("+38344987654"),
        "LIC-SM-01", Money.Create(60, "EUR"), 12, Specialty.GeneralPractice);

    private static AppointmentTime FutureSlot()
    {
        var d = DateTime.Now.Date.AddDays(14);
        while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) d = d.AddDays(1);
        return AppointmentTime.Create(d.AddHours(10));
    }

    private static Appointment NewPending()
    {
        var a = Appointment.Create(CreatePatient(), CreateDoctor(), FutureSlot(), LongReason, new AppointmentCodeGenerator());
        a.ClearDomainEvents();
        return a;
    }

    private static void ForcePastScheduledTime(Appointment appointment)
    {
        var past = DateTime.UtcNow.AddDays(-2).Date.AddHours(10);
        var time = AppointmentTime.FromPersistence(past);
        var field = typeof(Appointment).GetField(
            "<ScheduledTime>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field!.SetValue(appointment, time);
    }

    public static IEnumerable<object[]> IllegalTransitions()
    {
        // from, action name (for readability)
        yield return new object[] { AppointmentStatus.Pending, "Complete" };
        yield return new object[] { AppointmentStatus.Pending, "NoShow" };
        yield return new object[] { AppointmentStatus.Confirmed, "Confirm" };
        yield return new object[] { AppointmentStatus.Completed, "Confirm" };
        yield return new object[] { AppointmentStatus.Completed, "Cancel" };
        yield return new object[] { AppointmentStatus.Completed, "Complete" };
        yield return new object[] { AppointmentStatus.Completed, "NoShow" };
        yield return new object[] { AppointmentStatus.Cancelled, "Confirm" };
        yield return new object[] { AppointmentStatus.Cancelled, "Cancel" };
        yield return new object[] { AppointmentStatus.Cancelled, "Complete" };
        yield return new object[] { AppointmentStatus.Cancelled, "NoShow" };
        yield return new object[] { AppointmentStatus.NoShow, "Confirm" };
        yield return new object[] { AppointmentStatus.NoShow, "Cancel" };
        yield return new object[] { AppointmentStatus.NoShow, "Complete" };
        yield return new object[] { AppointmentStatus.NoShow, "NoShow" };
    }

    private static Appointment AtStatus(AppointmentStatus status)
    {
        var a = NewPending();
        switch (status)
        {
            case AppointmentStatus.Pending:
                break;
            case AppointmentStatus.Confirmed:
                a.Confirm();
                break;
            case AppointmentStatus.Completed:
                a.Confirm();
                a.Complete(LongNotes);
                break;
            case AppointmentStatus.Cancelled:
                a.Cancel(CancelReason);
                break;
            case AppointmentStatus.NoShow:
                a.Confirm();
                ForcePastScheduledTime(a);
                a.MarkAsNoShow();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        a.ClearDomainEvents();
        return a;
    }

    private static void Invoke(Appointment a, string action)
    {
        switch (action)
        {
            case "Confirm": a.Confirm(); break;
            case "Cancel": a.Cancel(CancelReason); break;
            case "Complete": a.Complete(LongNotes); break;
            case "NoShow":
                ForcePastScheduledTime(a);
                a.MarkAsNoShow();
                break;
            default: throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    [Theory]
    [MemberData(nameof(IllegalTransitions))]
    public void IllegalTransition_ThrowsInvalidAppointmentStateException(AppointmentStatus from, string action)
    {
        var appointment = AtStatus(from);
        var act = () => Invoke(appointment, action);
        act.Should().Throw<InvalidAppointmentStateException>(
            because: $"{from} → {action} must be blocked by the state machine");
        appointment.Status.Should().Be(from);
    }

    [Fact]
    public void LegalPath_Pending_Confirm_Complete_IsTerminal()
    {
        var a = NewPending();
        a.Status.Should().Be(AppointmentStatus.Pending);

        a.Confirm();
        a.Status.Should().Be(AppointmentStatus.Confirmed);
        a.DomainEvents.Should().ContainSingle(e => e is AppointmentConfirmedEvent);

        a.ClearDomainEvents();
        a.Complete(LongNotes);
        a.Status.Should().Be(AppointmentStatus.Completed);
        a.IsTerminal().Should().BeTrue();
        a.GetAllowedTransitions().Should().BeEmpty();
    }

    [Fact]
    public void LegalPath_Pending_Confirm_NoShow_RequiresPastTime()
    {
        var a = NewPending();
        a.Confirm();

        // Future slot → no-show forbidden (clinical correctness)
        var early = () => a.MarkAsNoShow();
        early.Should().Throw<InvalidOperationException>().WithMessage("*before the scheduled time*");

        ForcePastScheduledTime(a);
        a.MarkAsNoShow();
        a.Status.Should().Be(AppointmentStatus.NoShow);
        a.DomainEvents.Should().ContainSingle(e => e is AppointmentNoShowEvent);
        a.IsTerminal().Should().BeTrue();
    }

    [Fact]
    public void LegalPath_Pending_Cancel_IsTerminal()
    {
        var a = NewPending();
        a.Cancel(CancelReason);
        a.Status.Should().Be(AppointmentStatus.Cancelled);
        a.CancellationReason.Should().Be(CancelReason);
        a.IsTerminal().Should().BeTrue();
    }

    [Fact]
    public void LegalPath_Confirmed_Cancel_IsTerminal()
    {
        var a = NewPending();
        a.Confirm();
        a.Cancel(CancelReason);
        a.Status.Should().Be(AppointmentStatus.Cancelled);
        a.IsTerminal().Should().BeTrue();
    }

    [Fact]
    public void SoftDeleted_BlocksConfirm()
    {
        var a = NewPending();
        a.Delete();
        var act = () => a.Confirm();
        act.Should().Throw<InvalidOperationException>().WithMessage("*soft-deleted*");
        a.Status.Should().Be(AppointmentStatus.Pending);
    }

    [Fact]
    public void AllowedTransitions_MatchDocumentedGraph()
    {
        AtStatus(AppointmentStatus.Pending).GetAllowedTransitions()
            .Should().BeEquivalentTo(new[] { AppointmentStatus.Confirmed, AppointmentStatus.Cancelled });

        AtStatus(AppointmentStatus.Confirmed).GetAllowedTransitions()
            .Should().BeEquivalentTo(new[]
            {
                AppointmentStatus.Completed,
                AppointmentStatus.Cancelled,
                AppointmentStatus.NoShow
            });

        foreach (var terminal in new[] { AppointmentStatus.Completed, AppointmentStatus.Cancelled, AppointmentStatus.NoShow })
            AtStatus(terminal).GetAllowedTransitions().Should().BeEmpty();
    }

    [Fact]
    public void Confirm_PastAppointment_Throws()
    {
        var a = NewPending();
        ForcePastScheduledTime(a);
        var act = () => a.Confirm();
        act.Should().Throw<InvalidOperationException>().WithMessage("*past*");
    }
}
