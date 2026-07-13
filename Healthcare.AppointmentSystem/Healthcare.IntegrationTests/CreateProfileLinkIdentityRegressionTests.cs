using FluentAssertions;
using Healthcare.Application.Commands.CreateDoctor;
using Healthcare.Application.Commands.CreatePatient;
using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Enums;
using Healthcare.IntegrationTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Healthcare.IntegrationTests;

/// <summary>
/// Regression: User.PatientId / User.DoctorId must equal the SQL identity of the new profile
/// after CreatePatient / CreateDoctor. Linking before SaveChanges leaves Id=0 and breaks JWT claims.
/// </summary>
/// <remarks>
/// <para>
/// <b>CI gating (do not move back to UnitTests with Category=Integration):</b>
/// These tests use real EF Core identity generation (SQLite in-memory via
/// <see cref="EfCoreSqliteFixture"/>). Moq-only unit tests cannot catch the bug because mocks
/// never assign database-generated keys. They previously lived in Healthcare.UnitTests with
/// <c>[Trait("Category", "Integration")]</c>, but the CI unit job runs
/// <c>--filter Category!=Integration</c>, so they never executed on PRs. That is how the
/// PatientId=0 class of bug stayed green until production. Keep this class in
/// Healthcare.IntegrationTests so the <c>integration-tests</c> job in
/// <c>.github/workflows/ci.yml</c> always runs them (no Testcontainers required for this file).
/// </para>
/// <para>
/// Moq orchestration remains in UnitTests <c>CreatePatientHandlerTests</c> /
/// <c>CreateDoctorHandlerTests</c>.
/// </para>
/// </remarks>
public sealed class CreateProfileLinkIdentityRegressionTests
{
    [Fact]
    public async Task CreatePatient_PersistsUserPatientIdEqualToRealPatientIdentity()
    {
        await using var db = await EfCoreSqliteFixture.CreateAsync();
        await using var ctx = db.CreateContext();
        await ctx.Database.EnsureCreatedAsync();

        var userId = await EfCoreIdentityAssertions.SeedUserAsync(
            ctx,
            username: "link_patient_user",
            email: "link.patient.user@test.com",
            role: UserRole.Patient);

        var handler = new CreatePatientHandler(
            db.CreateUnitOfWork(ctx),
            Mock.Of<Healthcare.Application.Ports.Audit.IAuditLogService>());

        var result = await handler.HandleAsync(new CreatePatientCommand
        {
            FirstName = "Link",
            LastName = "Patient",
            Email = "link.patient.profile@test.com",
            PhoneNumber = "+355671111111",
            DateOfBirth = new DateTime(1990, 5, 15),
            Gender = "Female",
            Street = "1 Link St",
            City = "Tirana",
            State = "Tirana",
            PostalCode = "1001",
            Country = "Albania",
            RequestingUserId = userId,
        });

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().BeGreaterThan(0);

        await EfCoreIdentityAssertions.AssertUserPatientLinkAsync(db, userId, result.Value);
    }

    [Fact]
    public async Task CreateDoctor_PersistsUserDoctorIdEqualToRealDoctorIdentity()
    {
        await using var db = await EfCoreSqliteFixture.CreateAsync();
        await using var ctx = db.CreateContext();
        await ctx.Database.EnsureCreatedAsync();

        var userId = await EfCoreIdentityAssertions.SeedUserAsync(
            ctx,
            username: "link_doctor_user",
            email: "link.doctor.user@test.com",
            role: UserRole.Doctor);

        var handler = new CreateDoctorHandler(
            db.CreateUnitOfWork(ctx),
            Mock.Of<IDomainEventDispatcher>());

        var result = await handler.HandleAsync(new CreateDoctorCommand
        {
            FirstName = "Link",
            LastName = "Doctor",
            Email = "link.doctor.profile@test.com",
            PhoneNumber = "+355672222222",
            LicenseNumber = "LIC-LINK-001",
            Specialty = "Cardiology",
            ConsultationFeeAmount = 80m,
            ConsultationFeeCurrency = "USD",
            YearsOfExperience = 8,
            RequestingUserId = userId,
        });

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().BeGreaterThan(0);

        await EfCoreIdentityAssertions.AssertUserDoctorLinkAsync(db, userId, result.Value);
    }

    /// <summary>
    /// Documents the failure mode: if identity is not flushed before link, PatientId would be 0.
    /// This test exercises the happy path after the fix; Moq alone would not detect a regression.
    /// </summary>
    [Fact]
    public async Task CreatePatient_ReturnedIdMatchesDatabaseGeneratedPatientId()
    {
        await using var db = await EfCoreSqliteFixture.CreateAsync();
        await using var ctx = db.CreateContext();
        await ctx.Database.EnsureCreatedAsync();

        var userId = await EfCoreIdentityAssertions.SeedUserAsync(
            ctx, "id_match_user", "id.match@test.com", UserRole.Patient);

        var result = await new CreatePatientHandler(
            db.CreateUnitOfWork(ctx),
            Mock.Of<Healthcare.Application.Ports.Audit.IAuditLogService>()).HandleAsync(
            new CreatePatientCommand
            {
                FirstName = "Id",
                LastName = "Match",
                Email = "id.match.profile@test.com",
                PhoneNumber = "+355673333333",
                DateOfBirth = new DateTime(1988, 3, 3),
                Gender = "Male",
                Street = "2 Id St",
                City = "Pristina",
                State = "Pristina",
                PostalCode = "10000",
                Country = "Kosovo",
                RequestingUserId = userId,
            });

        result.IsSuccess.Should().BeTrue(result.Error);

        await using var verify = db.CreateContext();
        var patient = await verify.Patients.AsNoTracking()
            .SingleAsync(p => p.Id == result.Value);

        patient.Id.Should().Be(result.Value);
        patient.Id.Should().NotBe(0);
    }
}
