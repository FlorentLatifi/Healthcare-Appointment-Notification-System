using Healthcare.Domain.Common;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;

namespace Healthcare.Domain.Entities;

/// <summary>
/// Represents a user in the system (Patient, Doctor, or Admin).
/// </summary>
public sealed class User : Entity
{
    public string Username { get; private set; } = string.Empty;
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }

    // Foreign keys (optional - if User is a Patient or Doctor)
    public int? PatientId { get; private set; }
    public int? DoctorId { get; private set; }

    private User() { } // EF Core

    private User(string username, Email email, string passwordHash, UserRole role)
    {
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public static User Create(string username, Email email, string passwordHash, UserRole role)
    {
        Guard.AgainstNullOrWhiteSpace(username, nameof(username));
        Guard.AgainstNull(email, nameof(email));
        Guard.AgainstNullOrWhiteSpace(passwordHash, nameof(passwordHash));

        if (username.Length < 3)
            throw new ArgumentException("Username must be at least 3 characters");

        return new User(username, email, passwordHash, role);
    }

    public void LinkToPatient(int patientId)
    {
        PatientId = patientId;
        MarkAsModified();
    }

    public void LinkToDoctor(int doctorId)
    {
        DoctorId = doctorId;
        MarkAsModified();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkAsModified();
    }
}