using FluentAssertions;
using Healthcare.Adapters.Services;
using Healthcare.Application.Commands.BookAppointment;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Locking;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Services;
using Healthcare.UnitTests.Helpers;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Application.Commands;

/// <summary>
/// Critical production tests: double-booking and lock contention on the same doctor/time slot.
/// These protect the core revenue path against race conditions under multi-instance load.
/// </summary>
public sealed class BookAppointmentConcurrencyTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IDomainEventDispatcher> _dispatcher = new();
    private readonly Mock<IDistributedLockService> _locks = new();
    private readonly IAppointmentCodeGenerator _codes = new AppointmentCodeGenerator();

    private BookAppointmentHandler CreateHandler() =>
        new(_uow.Object, _dispatcher.Object, _locks.Object, _codes);

    private static DateTime FutureWeekdayAt(int hour)
    {
        var d = DateTime.Now.Date.AddDays(10);
        while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            d = d.AddDays(1);
        return d.AddHours(hour);
    }

    private void SetupHappyPathRepos(Patient patient, Doctor doctor, List<Appointment> existing)
    {
        var patients = new Mock<IPatientRepository>();
        patients.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        _uow.Setup(u => u.Patients).Returns(patients.Object);

        var doctors = new Mock<IDoctorRepository>();
        doctors.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(doctor);
        _uow.Setup(u => u.Doctors).Returns(doctors.Object);

        var appointments = new Mock<IAppointmentRepository>();
        appointments.Setup(r => r.GetByDoctorAndDateAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        appointments.Setup(r => r.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.Appointments).Returns(appointments.Object);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task HandleAsync_WhenLockNotAcquired_RejectsToPreventDoubleBookRace()
    {
        // Essential: if another instance holds the slot lock, we must fail closed (not book).
        var patient = TestDataBuilder.APatient().Build();
        var doctor = TestDataBuilder.ADoctor().Build();
        SetupHappyPathRepos(patient, doctor, new List<Appointment>());

        _locks.Setup(l => l.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ILockHandle?)null);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new BookAppointmentCommand
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            ScheduledTime = FutureWeekdayAt(10),
            Reason = "Race-condition booking attempt under load"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Another booking is in progress");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenSlotAlreadyBooked_RejectsSecondBooking()
    {
        // Essential: availability check under lock must block overlapping slots (domain IsAvailable).
        var patient = TestDataBuilder.APatient().Build();
        var doctor = TestDataBuilder.ADoctor().Build();
        var slot = FutureWeekdayAt(10);
        var existing = Appointment.Create(
            patient, doctor,
            Healthcare.Domain.ValueObjects.AppointmentTime.Create(slot),
            "Existing appointment occupying the slot now",
            _codes);

        SetupHappyPathRepos(patient, doctor, new List<Appointment> { existing });

        var handle = new Mock<ILockHandle>();
        handle.Setup(h => h.LockKey).Returns("lock");
        handle.Setup(h => h.DisposeAsync()).Returns(ValueTask.CompletedTask);
        _locks.Setup(l => l.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(handle.Object);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new BookAppointmentCommand
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            ScheduledTime = slot,
            Reason = "Second patient tries the same doctor time"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not available");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void DoctorIsAvailable_RejectsTimesInsideExclusiveThirtyMinuteWindow()
    {
        // Essential: domain conflict window (±30 exclusive) protects against near-overlaps
        // even when times come from persistence (not only Create() half-hour validation).
        var patient = TestDataBuilder.APatient().Build();
        var doctor = TestDataBuilder.ADoctor().Build();
        var baseLocal = FutureWeekdayAt(10);
        var existing = Appointment.Create(
            patient, doctor,
            Healthcare.Domain.ValueObjects.AppointmentTime.Create(baseLocal),
            "Existing appointment at ten o'clock sharp",
            _codes);

        // 10 minutes later (UTC-normalized via FromPersistence)
        var near = Healthcare.Domain.ValueObjects.AppointmentTime.FromPersistence(
            existing.ScheduledTime.Value.AddMinutes(10));

        doctor.IsAvailable(near, new[] { existing }).Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_AcquiresLockKeyScopedToDoctorAndTime()
    {
        // Essential: lock key must isolate by doctor + minute so only conflicting bookings contend.
        var patient = TestDataBuilder.APatient().Build();
        var doctor = TestDataBuilder.ADoctor().Build();
        var slot = FutureWeekdayAt(11);
        SetupHappyPathRepos(patient, doctor, new List<Appointment>());

        string? capturedKey = null;
        var handle = new Mock<ILockHandle>();
        handle.Setup(h => h.DisposeAsync()).Returns(ValueTask.CompletedTask);
        _locks.Setup(l => l.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<string, TimeSpan, CancellationToken>((k, _, _) => capturedKey = k)
            .ReturnsAsync(handle.Object);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new BookAppointmentCommand
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            ScheduledTime = slot,
            Reason = "Verify lock key shape for booking isolation"
        });

        result.IsSuccess.Should().BeTrue(result.Error);
        capturedKey.Should().NotBeNullOrEmpty();
        capturedKey.Should().Contain("doctor:");
        // AppointmentTime normalizes to UTC — lock key uses that value
        capturedKey.Should().MatchRegex(@"time:\d{12}");
    }
}
