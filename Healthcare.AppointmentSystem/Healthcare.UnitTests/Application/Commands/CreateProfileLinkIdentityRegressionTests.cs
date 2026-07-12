using FluentAssertions;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Adapters.Persistence.EntityFramework.Repositories;
using Healthcare.Application.Commands.CreateDoctor;
using Healthcare.Application.Commands.CreatePatient;
using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Healthcare.UnitTests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Healthcare.UnitTests.Application.Commands;

/// <summary>
/// Regression: User.PatientId / User.DoctorId must equal the SQL identity of the new profile
/// after CreatePatient / CreateDoctor. Linking before SaveChanges leaves Id=0 and breaks JWT claims.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CreateProfileLinkIdentityRegressionTests
{
    [Fact]
    public async Task CreatePatient_PersistsUserPatientIdEqualToRealPatientIdentity()
    {
        await using var db = await CreateSharedDatabaseAsync();
        await using var ctx = CreateContext(db.ConnectionString);
        await ctx.Database.EnsureCreatedAsync();

        var user = User.Create(
            "link_patient_user",
            Email.Create("link.patient.user@test.com"),
            "hash",
            UserRole.Patient);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var userId = user.Id;
        userId.Should().BeGreaterThan(0);

        var unitOfWork = CreateUnitOfWork(ctx);
        var handler = new CreatePatientHandler(unitOfWork);

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

        // Re-read from a clean context so we assert what was actually persisted.
        await using var verify = CreateContext(db.ConnectionString);
        var persistedUser = await verify.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
        var persistedPatient = await verify.Patients.AsNoTracking().SingleAsync(p => p.Id == result.Value);

        persistedUser.PatientId.Should().NotBeNull();
        persistedUser.PatientId.Should().NotBe(0, "identity must be assigned before LinkToPatient");
        persistedUser.PatientId.Should().Be(persistedPatient.Id);
        persistedUser.PatientId.Should().Be(result.Value);
    }

    [Fact]
    public async Task CreateDoctor_PersistsUserDoctorIdEqualToRealDoctorIdentity()
    {
        await using var db = await CreateSharedDatabaseAsync();
        await using var ctx = CreateContext(db.ConnectionString);
        await ctx.Database.EnsureCreatedAsync();

        var user = User.Create(
            "link_doctor_user",
            Email.Create("link.doctor.user@test.com"),
            "hash",
            UserRole.Doctor);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var userId = user.Id;
        userId.Should().BeGreaterThan(0);

        var unitOfWork = CreateUnitOfWork(ctx);
        var dispatcher = new Mock<IDomainEventDispatcher>();
        var handler = new CreateDoctorHandler(unitOfWork, dispatcher.Object);

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

        await using var verify = CreateContext(db.ConnectionString);
        var persistedUser = await verify.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
        var persistedDoctor = await verify.Doctors.AsNoTracking().SingleAsync(d => d.Id == result.Value);

        persistedUser.DoctorId.Should().NotBeNull();
        persistedUser.DoctorId.Should().NotBe(0, "identity must be assigned before LinkToDoctor");
        persistedUser.DoctorId.Should().Be(persistedDoctor.Id);
        persistedUser.DoctorId.Should().Be(result.Value);
    }

    private static EFCoreUnitOfWork CreateUnitOfWork(HealthcareDbContext ctx) =>
        new(
            ctx,
            new EFCoreAppointmentRepository(ctx),
            new EFCorePatientRepository(ctx),
            new EFCoreDoctorRepository(ctx),
            new EFCoreUserRepository(ctx),
            new EFCorePaymentRepository(ctx),
            new EFCoreAuditLogRepository(ctx),
            new EFCoreUserSessionRepository(ctx));

    private static SqliteCompatibleDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlite(connectionString)
            .Options);

    private static async Task<SharedDatabase> CreateSharedDatabaseAsync()
    {
        var connectionString = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();
        return new SharedDatabase(keepAlive, connectionString);
    }

    private sealed class SharedDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _keepAlive;

        public SharedDatabase(SqliteConnection keepAlive, string connectionString)
        {
            _keepAlive = keepAlive;
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }

        public async ValueTask DisposeAsync()
        {
            await _keepAlive.DisposeAsync();
        }
    }
}
