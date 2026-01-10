using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;

namespace Healthcare.UnitTests.Helpers;

/// <summary>
/// Builder pattern for creating test data.
/// </summary>
/// <remarks>
/// This class makes tests more readable and maintainable by:
/// - Providing sensible defaults
/// - Allowing customization via fluent API
/// - Centralizing test data creation
/// </remarks>
public class TestDataBuilder
{
    #region Patient Builder

    public class PatientBuilder
    {
        private string _firstName = "John";
        private string _lastName = "Doe";
        private string _email = "patient@test.com";
        private string _phone = "+38349123456";
        private DateTime _dateOfBirth = DateTime.Today.AddYears(-30);
        private Gender _gender = Gender.Male;
        private string _street = "123 Main St";
        private string _city = "Pristina";
        private string _state = "Kosovo";
        private string _postalCode = "10000";
        private string _country = "Kosovo";

        public PatientBuilder WithName(string firstName, string lastName)
        {
            _firstName = firstName;
            _lastName = lastName;
            return this;
        }

        public PatientBuilder WithEmail(string email)
        {
            _email = email;
            return this;
        }

        public PatientBuilder WithPhone(string phone)
        {
            _phone = phone;
            return this;
        }

        public PatientBuilder WithDateOfBirth(DateTime dateOfBirth)
        {
            _dateOfBirth = dateOfBirth;
            return this;
        }

        public PatientBuilder WithGender(Gender gender)
        {
            _gender = gender;
            return this;
        }

        public PatientBuilder WithAge(int age)
        {
            _dateOfBirth = DateTime.Today.AddYears(-age);
            return this;
        }

        public PatientBuilder WithAddress(string street, string city, string state, string postalCode, string country)
        {
            _street = street;
            _city = city;
            _state = state;
            _postalCode = postalCode;
            _country = country;
            return this;
        }

        public Patient Build()
        {
            var email = Email.Create(_email);
            var phone = PhoneNumber.Create(_phone);
            var address = Address.Create(_street, _city, _state, _postalCode, _country);

            return Patient.Create(
                _firstName,
                _lastName,
                email,
                phone,
                _dateOfBirth,
                _gender,
                address);
        }
    }

    #endregion

    #region Doctor Builder

    public class DoctorBuilder
    {
        private string _firstName = "Jane";
        private string _lastName = "Smith";
        private string _email = "doctor@test.com";
        private string _phone = "+38349987654";
        private string _licenseNumber = "LIC-12345";
        private decimal _feeAmount = 50;
        private string _feeCurrency = "USD";
        private int _yearsOfExperience = 10;
        private Specialty _primarySpecialty = Specialty.GeneralPractice;

        public DoctorBuilder WithName(string firstName, string lastName)
        {
            _firstName = firstName;
            _lastName = lastName;
            return this;
        }

        public DoctorBuilder WithEmail(string email)
        {
            _email = email;
            return this;
        }

        public DoctorBuilder WithPhone(string phone)
        {
            _phone = phone;
            return this;
        }

        public DoctorBuilder WithLicense(string licenseNumber)
        {
            _licenseNumber = licenseNumber;
            return this;
        }

        public DoctorBuilder WithConsultationFee(decimal amount, string currency = "USD")
        {
            _feeAmount = amount;
            _feeCurrency = currency;
            return this;
        }

        public DoctorBuilder WithExperience(int years)
        {
            _yearsOfExperience = years;
            return this;
        }

        public DoctorBuilder WithSpecialty(Specialty specialty)
        {
            _primarySpecialty = specialty;
            return this;
        }

        public Doctor Build()
        {
            var email = Email.Create(_email);
            var phone = PhoneNumber.Create(_phone);
            var fee = Money.Create(_feeAmount, _feeCurrency);

            return Doctor.Create(
                _firstName,
                _lastName,
                email,
                phone,
                _licenseNumber,
                fee,
                _yearsOfExperience,
                _primarySpecialty);
        }
    }

    #endregion

    #region Appointment Builder

    public class AppointmentBuilder
    {
        private Patient? _patient;
        private Doctor? _doctor;
        private DateTime _scheduledTime;
        private string _reason = "Annual checkup and consultation with the doctor";

        public AppointmentBuilder()
        {
            // Default: Next Monday at 10:00 AM
            _scheduledTime = GetNextWeekday(DayOfWeek.Monday)
                .Date.AddHours(10);
        }

        public AppointmentBuilder WithPatient(Patient patient)
        {
            _patient = patient;
            return this;
        }

        public AppointmentBuilder WithDoctor(Doctor doctor)
        {
            _doctor = doctor;
            return this;
        }

        public AppointmentBuilder WithScheduledTime(DateTime scheduledTime)
        {
            _scheduledTime = scheduledTime;
            return this;
        }

        public AppointmentBuilder WithReason(string reason)
        {
            _reason = reason;
            return this;
        }

        public AppointmentBuilder ScheduledForNextWeek()
        {
            _scheduledTime = GetNextWeekday(DayOfWeek.Monday)
                .AddDays(7).Date.AddHours(10);
            return this;
        }

        public AppointmentBuilder ScheduledForTomorrow()
        {
            var tomorrow = DateTime.Today.AddDays(1);
            while (tomorrow.DayOfWeek == DayOfWeek.Saturday ||
                   tomorrow.DayOfWeek == DayOfWeek.Sunday)
            {
                tomorrow = tomorrow.AddDays(1);
            }
            _scheduledTime = tomorrow.AddHours(10);
            return this;
        }

        public Appointment Build()
        {
            var patient = _patient ?? new PatientBuilder().Build();
            var doctor = _doctor ?? new DoctorBuilder().Build();
            var appointmentTime = AppointmentTime.Create(_scheduledTime);

            return Appointment.Create(patient, doctor, appointmentTime, _reason);
        }

        private static DateTime GetNextWeekday(DayOfWeek targetDay)
        {
            var today = DateTime.Today;
            var daysUntilTarget = ((int)targetDay - (int)today.DayOfWeek + 7) % 7;

            if (daysUntilTarget == 0)
            {
                daysUntilTarget = 7;
            }

            return today.AddDays(daysUntilTarget);
        }
    }

    #endregion

    #region Static Factory Methods

    /// <summary>
    /// Creates a default patient builder.
    /// </summary>
    public static PatientBuilder APatient() => new();

    /// <summary>
    /// Creates a default doctor builder.
    /// </summary>
    public static DoctorBuilder ADoctor() => new();

    /// <summary>
    /// Creates a default appointment builder.
    /// </summary>
    public static AppointmentBuilder AnAppointment() => new();

    #endregion

    #region Common Test Scenarios

    /// <summary>
    /// Creates a minor patient (under 18).
    /// </summary>
    public static Patient CreateMinorPatient()
    {
        return APatient()
            .WithName("Child", "Patient")
            .WithAge(15)
            .Build();
    }

    /// <summary>
    /// Creates a senior patient (65+).
    /// </summary>
    public static Patient CreateSeniorPatient()
    {
        return APatient()
            .WithName("Senior", "Patient")
            .WithAge(70)
            .Build();
    }

    /// <summary>
    /// Creates an experienced doctor (10+ years).
    /// </summary>
    public static Doctor CreateExperiencedDoctor()
    {
        return ADoctor()
            .WithName("Experienced", "Doctor")
            .WithExperience(15)
            .WithSpecialty(Specialty.Cardiology)
            .Build();
    }

    /// <summary>
    /// Creates a new doctor (less than 10 years).
    /// </summary>
    public static Doctor CreateNewDoctor()
    {
        return ADoctor()
            .WithName("New", "Doctor")
            .WithExperience(3)
            .WithSpecialty(Specialty.GeneralPractice)
            .Build();
    }

    /// <summary>
    /// Creates a confirmed appointment ready for completion.
    /// </summary>
    public static Appointment CreateConfirmedAppointment()
    {
        var appointment = AnAppointment().Build();
        appointment.Confirm();
        return appointment;
    }

    #endregion
}