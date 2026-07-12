using FluentAssertions;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.UnitTests.Helpers;

/// <summary>
/// Assertions for scenarios where a scalar link/FK must equal a database-generated identity.
/// Forces a re-read on a fresh context so the test does not pass via change-tracker coincidence.
/// </summary>
public static class EfCoreIdentityAssertions
{
    /// <summary>
    /// Verifies <see cref="User.PatientId"/> equals the real <see cref="Patient.Id"/> after
    /// profile creation (the bug Moq missed when LinkToPatient ran before identity flush).
    /// </summary>
    public static async Task AssertUserPatientLinkAsync(
        EfCoreSqliteFixture db,
        int userId,
        int expectedPatientId,
        CancellationToken cancellationToken = default)
    {
        expectedPatientId.Should().BeGreaterThan(0, "patient identity must be database-generated");

        await using var verify = db.CreateContext();
        var user = await verify.Users.AsNoTracking()
            .SingleAsync(u => u.Id == userId, cancellationToken);
        var patient = await verify.Patients.AsNoTracking()
            .SingleAsync(p => p.Id == expectedPatientId, cancellationToken);

        user.PatientId.Should().NotBeNull("user must be linked to a patient");
        user.PatientId.Should().NotBe(0, "identity must be assigned before LinkToPatient");
        user.PatientId.Should().Be(patient.Id);
        user.PatientId.Should().Be(expectedPatientId);
    }

    /// <summary>
    /// Verifies <see cref="User.DoctorId"/> equals the real <see cref="Doctor.Id"/> after
    /// profile creation.
    /// </summary>
    public static async Task AssertUserDoctorLinkAsync(
        EfCoreSqliteFixture db,
        int userId,
        int expectedDoctorId,
        CancellationToken cancellationToken = default)
    {
        expectedDoctorId.Should().BeGreaterThan(0, "doctor identity must be database-generated");

        await using var verify = db.CreateContext();
        var user = await verify.Users.AsNoTracking()
            .SingleAsync(u => u.Id == userId, cancellationToken);
        var doctor = await verify.Doctors.AsNoTracking()
            .SingleAsync(d => d.Id == expectedDoctorId, cancellationToken);

        user.DoctorId.Should().NotBeNull("user must be linked to a doctor");
        user.DoctorId.Should().NotBe(0, "identity must be assigned before LinkToDoctor");
        user.DoctorId.Should().Be(doctor.Id);
        user.DoctorId.Should().Be(expectedDoctorId);
    }

    /// <summary>
    /// Seeds a user with a real identity, returns the generated user Id.
    /// </summary>
    public static async Task<int> SeedUserAsync(
        HealthcareDbContext context,
        string username,
        string email,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        var user = User.Create(
            username,
            Email.Create(email),
            "hash",
            role);
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        user.Id.Should().BeGreaterThan(0, "user identity should be generated after SaveChanges");
        return user.Id;
    }
}
