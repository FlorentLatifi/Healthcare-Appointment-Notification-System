using Healthcare.Domain.Common;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;

namespace Healthcare.Domain.Entities;

/// <summary>
/// Represents a patient in the healthcare system.
/// </summary>
/// <remarks>
/// Design Pattern: Rich Domain Model (NOT Anemic)
/// Factory Method Pattern: Use Create() factory method instead of constructor.
/// </remarks>
public sealed class Patient : Entity
{
    /// <summary>
    /// Gets the patient's first name.
    /// </summary>
    public string FirstName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the patient's last name.
    /// </summary>
    public string LastName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the patient's email address.
    /// </summary>
    public Email Email { get; private set; } = null!;

    /// <summary>
    /// Gets the patient's phone number.
    /// </summary>
    public PhoneNumber PhoneNumber { get; private set; } = null!;

    /// <summary>
    /// Gets the patient's date of birth.
    /// </summary>
    public DateTime DateOfBirth { get; private set; }

    /// <summary>
    /// Gets the patient's gender.
    /// </summary>
    public Gender Gender { get; private set; }

    /// <summary>
    /// Gets the patient's residential address.
    /// </summary>
    public Address Address { get; private set; } = null!;

    /// <summary>
    /// Gets a value indicating whether the patient account is active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets the patient's full name.
    /// </summary>
    public string FullName => $"{FirstName} {LastName}";

    /// <summary>
    /// Gets the patient's age in years.
    /// </summary>
    public int Age
    {
        get
        {
            var today = DateTime.Today;
            var age = today.Year - DateOfBirth.Year;
            if (DateOfBirth.Date > today.AddYears(-age)) age--;
            return age;
        }
    }

    // Private parameterless constructor for EF Core
    private Patient() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Patient"/> class.
    /// Private constructor - use Create() factory method.
    /// </summary>
    private Patient(
        string firstName,
        string lastName,
        Email email,
        PhoneNumber phoneNumber,
        DateTime dateOfBirth,
        Gender gender,
        Address address)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        PhoneNumber = phoneNumber;
        DateOfBirth = dateOfBirth;
        Gender = gender;
        Address = address;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a new Patient using the Factory Method pattern.
    /// </summary>
    public static Patient Create(
        string firstName,
        string lastName,
        Email email,
        PhoneNumber phoneNumber,
        DateTime dateOfBirth,
        Gender gender,
        Address address)
    {
        // Validation
        Guard.AgainstNullOrWhiteSpace(firstName, nameof(firstName));
        Guard.AgainstNullOrWhiteSpace(lastName, nameof(lastName));
        Guard.AgainstNull(email, nameof(email));
        Guard.AgainstNull(phoneNumber, nameof(phoneNumber));
        Guard.AgainstNull(address, nameof(address));

        // Business Rule: Patient must be at least 1 day old
        if (dateOfBirth >= DateTime.Today)
        {
            throw new ArgumentException("Date of birth must be in the past.", nameof(dateOfBirth));
        }

        // Business Rule: Patient cannot be more than 150 years old
        var age = DateTime.Today.Year - dateOfBirth.Year;
        if (age > 150)
        {
            throw new ArgumentException("Invalid date of birth - patient cannot be over 150 years old.", nameof(dateOfBirth));
        }

        return new Patient(
            firstName.Trim(),
            lastName.Trim(),
            email,
            phoneNumber,
            dateOfBirth.Date,
            gender,
            address);
    }

    /// <summary>
    /// Updates the patient's contact information.
    /// </summary>
    public void UpdateContactInformation(Email email, PhoneNumber phoneNumber, Address address)
    {
        Guard.AgainstNull(email, nameof(email));
        Guard.AgainstNull(phoneNumber, nameof(phoneNumber));
        Guard.AgainstNull(address, nameof(address));

        Email = email;
        PhoneNumber = phoneNumber;
        Address = address;

        MarkAsModified();
    }

    /// <summary>
    /// Updates the patient's personal information.
    /// </summary>
    public void UpdatePersonalInformation(string firstName, string lastName)
    {
        Guard.AgainstNullOrWhiteSpace(firstName, nameof(firstName));
        Guard.AgainstNullOrWhiteSpace(lastName, nameof(lastName));

        FirstName = firstName.Trim();
        LastName = lastName.Trim();

        MarkAsModified();
    }

    /// <summary>
    /// Deactivates the patient account.
    /// </summary>
    public void Deactivate()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Patient account is already deactivated.");
        }

        IsActive = false;
        MarkAsModified();
    }

    /// <summary>
    /// Reactivates a previously deactivated patient account.
    /// </summary>
    public void Reactivate()
    {
        if (IsActive)
        {
            throw new InvalidOperationException("Patient account is already active.");
        }

        IsActive = true;
        MarkAsModified();
    }

    /// <summary>
    /// Checks if the patient is a minor (under 18 years old).
    /// </summary>
    public bool IsMinor() => Age < 18;

    /// <summary>
    /// Checks if the patient is a senior (65 years or older).
    /// </summary>
    public bool IsSenior() => Age >= 65;
}