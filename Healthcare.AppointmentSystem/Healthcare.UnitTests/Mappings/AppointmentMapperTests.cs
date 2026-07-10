using FluentAssertions;
using Healthcare.Application.Mappings;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Healthcare.Adapters.Services;
using Healthcare.UnitTests.Helpers;

namespace Healthcare.UnitTests.Mappings;

public sealed class AppointmentMapperTests
{
    private static DateTime FutureLocalTime()
    {
        var dt = DateTime.Now.AddDays(7);
        return new DateTime(dt.Year, dt.Month, dt.Day, 10, 0, 0, DateTimeKind.Local);
    }

    [Fact]
    public void ToDto_ShouldMapAllFieldsCorrectly()
    {
        var patient = TestDataBuilder.APatient()
            .WithName("John", "Doe")
            .WithEmail("patient@test.com")
            .WithPhone("+38349123456")
            .WithDateOfBirth(new DateTime(1990, 6, 15))
            .WithGender(Gender.Male)
            .WithAddress("123 Main St", "Pristina", "Kosovo", "10000", "Kosovo")
            .Build();

        var doctor = TestDataBuilder.ADoctor()
            .WithName("Jane", "Smith")
            .WithEmail("doctor@test.com")
            .WithPhone("+38349987654")
            .WithLicense("LIC-12345")
            .WithConsultationFee(75, "EUR")
            .WithExperience(12)
            .WithSpecialty(Specialty.Cardiology)
            .Build();

        var localTime = FutureLocalTime();
        var appointmentTime = AppointmentTime.Create(localTime);
        var appointment = Appointment.Create(
            patient, doctor, appointmentTime,
            "Routine cardiology checkup",
            new AppointmentCodeGenerator());

        appointment.Confirm();
        var dto = AppointmentMapper.ToDto(appointment);

        dto.Id.Should().Be(appointment.Id);
        dto.ReferenceCode.Should().Be(appointment.ReferenceCode);
        dto.PatientId.Should().Be(appointment.PatientId);
        dto.DoctorId.Should().Be(appointment.DoctorId);

        dto.Patient.Id.Should().Be(patient.Id);
        dto.Patient.FirstName.Should().Be("John");
        dto.Patient.LastName.Should().Be("Doe");
        dto.Patient.FullName.Should().Be(patient.FullName);
        dto.Patient.Email.Should().Be("patient@test.com");
        dto.Patient.PhoneNumber.Should().Be("+38349123456");
        dto.Patient.DateOfBirth.Should().Be(new DateTime(1990, 6, 15));
        dto.Patient.Age.Should().Be(patient.Age);
        dto.Patient.Gender.Should().Be("Male");
        dto.Patient.Address.Should().Be(patient.Address.GetFullAddress());
        dto.Patient.IsActive.Should().BeTrue();
        dto.Patient.CreatedAt.Should().Be(patient.CreatedAt);

        dto.Doctor.Id.Should().Be(doctor.Id);
        dto.Doctor.FirstName.Should().Be("Jane");
        dto.Doctor.LastName.Should().Be("Smith");
        dto.Doctor.FullName.Should().Be(doctor.FullName);
        dto.Doctor.Email.Should().Be("doctor@test.com");
        dto.Doctor.PhoneNumber.Should().Be("+38349987654");
        dto.Doctor.LicenseNumber.Should().Be("LIC-12345");
        dto.Doctor.Specialties.Should().ContainSingle().Which.Should().Be("Cardiology");
        dto.Doctor.ConsultationFeeAmount.Should().Be(75);
        dto.Doctor.ConsultationFeeCurrency.Should().Be("EUR");
        dto.Doctor.IsAcceptingPatients.Should().BeTrue();
        dto.Doctor.IsActive.Should().BeTrue();
        dto.Doctor.YearsOfExperience.Should().Be(12);
        dto.Doctor.CreatedAt.Should().Be(doctor.CreatedAt);

        dto.ScheduledTime.Should().Be(appointment.ScheduledTime.Value);
        dto.ScheduledTime.Kind.Should().Be(DateTimeKind.Utc);
        dto.ScheduledDate.Should().Be(localTime.ToString("yyyy-MM-dd"));
        dto.ScheduledTimeFormatted.Should().NotBeNullOrEmpty();
        dto.Status.Should().Be("Confirmed");
        dto.Reason.Should().Be("Routine cardiology checkup");
        dto.DoctorNotes.Should().BeNull();
        dto.CancellationReason.Should().BeNull();
        dto.ConsultationFeeAmount.Should().Be(75);
        dto.ConsultationFeeCurrency.Should().Be("EUR");
        dto.ConfirmedAt.Should().NotBeNull();
        dto.CompletedAt.Should().BeNull();
        dto.CancelledAt.Should().BeNull();
        dto.CreatedAt.Should().Be(appointment.CreatedAt);
    }

    [Fact]
    public void ToDto_WithNullPatient_ShouldThrow()
    {
        var doctor = TestDataBuilder.ADoctor().Build();
        var appointmentTime = AppointmentTime.Create(FutureLocalTime());
        var appointment = Appointment.Create(
            TestDataBuilder.APatient().Build(),
            doctor,
            appointmentTime,
            "Test reason",
            new AppointmentCodeGenerator());

        var field = appointment.GetType()
            .GetField("<Patient>k__BackingField",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(appointment, null);

        var act = () => AppointmentMapper.ToDto(appointment);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Appointment must have Patient and Doctor loaded.");
    }

    [Fact]
    public void ToDto_WithNullDoctor_ShouldThrow()
    {
        var patient = TestDataBuilder.APatient().Build();
        var appointmentTime = AppointmentTime.Create(FutureLocalTime());
        var appointment = Appointment.Create(
            patient,
            TestDataBuilder.ADoctor().Build(),
            appointmentTime,
            "Test reason",
            new AppointmentCodeGenerator());

        var field = appointment.GetType()
            .GetField("<Doctor>k__BackingField",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(appointment, null);

        var act = () => AppointmentMapper.ToDto(appointment);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Appointment must have Patient and Doctor loaded.");
    }
}
