using FluentAssertions;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Adapters.Persistence.EntityFramework.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Healthcare.UnitTests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.UnitTests.Adapters.Persistence.EntityFramework;

public sealed class EFCoreEmailRepositoryTests
{
    [Fact]
    public async Task Doctor_GetByEmailAsync_TranslatesValueObjectComparisonToSql()
    {
        await using var database = await CreateDatabaseAsync();
        var context = database.Context;
        var repository = new EFCoreDoctorRepository(context);
        var doctor = TestDataBuilder.ADoctor()
            .WithEmail("doctor.integration@test.com")
            .WithLicense("DOC-10001")
            .Build();

        await repository.AddAsync(doctor);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await repository.GetByEmailAsync("DOCTOR.INTEGRATION@test.com");

        result.Should().NotBeNull();
        result!.Email.Value.Should().Be("doctor.integration@test.com");
        (await repository.ExistsAsync("DOCTOR.INTEGRATION@test.com")).Should().BeTrue();
    }

    [Fact]
    public async Task Patient_GetByEmailAsync_TranslatesValueObjectComparisonToSql()
    {
        await using var database = await CreateDatabaseAsync();
        var context = database.Context;
        var repository = new EFCorePatientRepository(context);
        var patient = TestDataBuilder.APatient()
            .WithEmail("patient.integration@test.com")
            .Build();

        await repository.AddAsync(patient);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await repository.GetByEmailAsync("PATIENT.INTEGRATION@test.com");

        result.Should().NotBeNull();
        result!.Email.Value.Should().Be("patient.integration@test.com");
        (await repository.ExistsAsync("PATIENT.INTEGRATION@test.com")).Should().BeTrue();
    }

    [Fact]
    public async Task User_GetByEmailAsync_TranslatesValueObjectComparisonToSql()
    {
        await using var database = await CreateDatabaseAsync();
        var context = database.Context;
        var repository = new EFCoreUserRepository(context);
        var user = User.Create(
            "integration-user",
            Email.Create("user.integration@test.com"),
            "hashed-password",
            UserRole.Patient);

        await repository.AddAsync(user);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await repository.GetByEmailAsync("USER.INTEGRATION@test.com");

        result.Should().NotBeNull();
        result!.Email.Value.Should().Be("user.integration@test.com");
    }

    private static async Task<TestDatabase> CreateDatabaseAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new HealthcareDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return new TestDatabase(context, connection);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public TestDatabase(HealthcareDbContext context, SqliteConnection connection)
        {
            Context = context;
            _connection = connection;
        }

        public HealthcareDbContext Context { get; }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
