using Healthcare.Domain.Common;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;

namespace Healthcare.Domain.Entities;

/// <summary>
/// Represents a doctor in the healthcare system.
/// </summary>
/// <remarks>
/// Design Pattern: Rich Domain Model + Strategy Pattern (availability checking)
/// </remarks>
public sealed class Doctor : Entity
{
    private readonly List<Specialty> _specialties = new();

    /// <summary>
    /// Gets the doctor's first name.
    /// </summary>
    public string FirstName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the doctor's last name.
    /// </summary>
    public string LastName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the doctor's email address.
    /// </summary>
    public Email Email { get; private set; } = null!;

    /// <summary>
    /// Gets the doctor's phone number.
    /// </summary>
    public PhoneNumber PhoneNumber { get; private set; } = null!;

    /// <summary>
    /// Gets the doctor's medical license number.
    /// </summary>
    public string LicenseNumber { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the doctor's specialties.
    /// </summary>
    public IReadOnlyCollection<Specialty> Specialties => _specialties.AsReadOnly();

    /// <summary>
    /// Gets the doctor's consultation fee.
    /// </summary>
    public Money ConsultationFee { get; private set; } = null!;

    /// <summary>
    /// Gets a value indicating whether the doctor is currently accepting new patients.
    /// </summary>
    public bool IsAcceptingPatients { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the doctor account is active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets the doctor's years of experience.
    /// </summary>
    public int YearsOfExperience { get; private set; }

    /// <summary>
    /// Gets the doctor's full name with title.
    /// </summary>
    public string FullName => $"Dr. {FirstName} {LastName}";

    // Private parameterless constructor for EF Core
    private Doctor() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Doctor"/> class.
    /// Private constructor - use Create() factory method.
    /// </summary>
    private Doctor(
        string firstName,
        string lastName,
        Email email,
        PhoneNumber phoneNumber,
        string licenseNumber,
        Money consultationFee,
        int yearsOfExperience)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        PhoneNumber = phoneNumber;
        LicenseNumber = licenseNumber;
        ConsultationFee = consultationFee;
        YearsOfExperience = yearsOfExperience;
        IsAcceptingPatients = true;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a new Doctor using the Factory Method pattern.
    /// </summary>
    public static Doctor Create(
        string firstName,
        string lastName,
        Email email,
        PhoneNumber phoneNumber,
        string licenseNumber,
        Money consultationFee,
        int yearsOfExperience,
        Specialty primarySpecialty)
    {
        // Validation
        Guard.AgainstNullOrWhiteSpace(firstName, nameof(firstName));
        Guard.AgainstNullOrWhiteSpace(lastName, nameof(lastName));
        Guard.AgainstNull(email, nameof(email));
        Guard.AgainstNull(phoneNumber, nameof(phoneNumber));
        Guard.AgainstNullOrWhiteSpace(licenseNumber, nameof(licenseNumber));
        Guard.AgainstNull(consultationFee, nameof(consultationFee));

        if (yearsOfExperience < 0)
        {
            throw new ArgumentException("Years of experience cannot be negative.", nameof(yearsOfExperience));
        }

        if (yearsOfExperience > 70)
        {
            throw new ArgumentException("Years of experience cannot exceed 70.", nameof(yearsOfExperience));
        }

        if (licenseNumber.Trim().Length < 5)
        {
            throw new ArgumentException("License number must be at least 5 characters.", nameof(licenseNumber));
        }

        var doctor = new Doctor(
            firstName.Trim(),
            lastName.Trim(),
            email,
            phoneNumber,
            licenseNumber.Trim().ToUpperInvariant(),
            consultationFee,
            yearsOfExperience);

        doctor._specialties.Add(primarySpecialty);

        return doctor;
    }

    /// <summary>
    /// Adds an additional specialty to the doctor's expertise.
    /// </summary>
    public void AddSpecialty(Specialty specialty)
    {
        if (_specialties.Contains(specialty))
        {
            throw new InvalidOperationException($"Doctor already has {specialty} specialty.");
        }

        if (_specialties.Count >= 3)
        {
            throw new InvalidOperationException("Doctor cannot have more than 3 specialties.");
        }

        _specialties.Add(specialty);
        MarkAsModified();
    }

    /// <summary>
    /// Removes a specialty from the doctor's expertise.
    /// </summary>
    public void RemoveSpecialty(Specialty specialty)
    {
        if (_specialties.Count <= 1)
        {
            throw new InvalidOperationException("Doctor must have at least one specialty.");
        }

        if (!_specialties.Contains(specialty))
        {
            throw new InvalidOperationException($"Doctor does not have {specialty} specialty.");
        }

        _specialties.Remove(specialty);
        MarkAsModified();
    }

    /// <summary>
    /// Updates the doctor's consultation fee.
    /// </summary>
    public void UpdateConsultationFee(Money newFee)
    {
        Guard.AgainstNull(newFee, nameof(newFee));

        if (newFee < ConsultationFee * 0.5m)
        {
            throw new InvalidOperationException("Consultation fee cannot be reduced by more than 50% at once.");
        }

        ConsultationFee = newFee;
        MarkAsModified();
    }

    /// <summary>
    /// Updates the doctor's contact information.
    /// </summary>
    public void UpdateContactInformation(Email email, PhoneNumber phoneNumber)
    {
        Guard.AgainstNull(email, nameof(email));
        Guard.AgainstNull(phoneNumber, nameof(phoneNumber));

        Email = email;
        PhoneNumber = phoneNumber;
        MarkAsModified();
    }

    /// <summary>
    /// Stops accepting new patients.
    /// </summary>
    public void StopAcceptingPatients()
    {
        if (!IsAcceptingPatients)
        {
            throw new InvalidOperationException("Doctor is already not accepting patients.");
        }

        IsAcceptingPatients = false;
        MarkAsModified();
    }

    /// <summary>
    /// Starts accepting new patients.
    /// </summary>
    public void StartAcceptingPatients()
    {
        if (IsAcceptingPatients)
        {
            throw new InvalidOperationException("Doctor is already accepting patients.");
        }

        if (!IsActive)
        {
            throw new InvalidOperationException("Cannot accept patients - doctor account is inactive.");
        }

        IsAcceptingPatients = true;
        MarkAsModified();
    }

    /// <summary>
    /// Deactivates the doctor account.
    /// </summary>
    public void Deactivate()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Doctor account is already deactivated.");
        }

        IsActive = false;
        IsAcceptingPatients = false;
        MarkAsModified();
    }

    /// <summary>
    /// Reactivates the doctor account.
    /// </summary>
    public void Reactivate()
    {
        if (IsActive)
        {
            throw new InvalidOperationException("Doctor account is already active.");
        }

        IsActive = true;
        MarkAsModified();
    }

    /// <summary>
    /// Checks if the doctor is available at the specified time.
    /// </summary>
    public bool IsAvailable(AppointmentTime appointmentTime, IEnumerable<Appointment> existingAppointments)
    {
        Guard.AgainstNull(appointmentTime, nameof(appointmentTime));
        Guard.AgainstNull(existingAppointments, nameof(existingAppointments));

        if (!IsActive)
        {
            return false;
        }

        if (!IsAcceptingPatients)
        {
            return false;
        }

        var requestedTime = appointmentTime.Value;
        var thirtyMinutesBefore = requestedTime.AddMinutes(-30);
        var thirtyMinutesAfter = requestedTime.AddMinutes(30);

        var hasConflict = existingAppointments.Any(apt =>
            apt.Status != AppointmentStatus.Cancelled &&
            apt.Status != AppointmentStatus.NoShow &&
            apt.ScheduledTime.Value > thirtyMinutesBefore &&
            apt.ScheduledTime.Value < thirtyMinutesAfter);

        return !hasConflict;
    }

    /// <summary>
    /// Checks if the doctor has a specific specialty.
    /// </summary>
    public bool HasSpecialty(Specialty specialty) => _specialties.Contains(specialty);

    /// <summary>
    /// Checks if the doctor is experienced (10+ years).
    /// </summary>
    public bool IsExperienced() => YearsOfExperience >= 10;
}