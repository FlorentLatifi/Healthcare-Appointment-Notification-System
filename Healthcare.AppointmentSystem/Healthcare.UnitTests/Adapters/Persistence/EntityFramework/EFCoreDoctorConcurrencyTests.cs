using FluentAssertions;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Healthcare.UnitTests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.UnitTests.Adapters.Persistence.EntityFramework;

[Trait("Category", "Integration")]
public sealed class EFCoreDoctorConcurrencyTests
{
    [Fact]
    public async Task SaveChangesAsync_ConcurrentDoctorUpdates_ThrowsDbUpdateConcurrencyException()
    {
        await using var sharedDb = await CreateSharedDatabaseAsync();
        var connectionString = sharedDb.ConnectionString;

        var doctorId = await SeedDoctorAsync(connectionString);

        await using var ctx1 = new SqliteCompatibleDbContext(CreateOptions(connectionString));
        await using var ctx2 = new SqliteCompatibleDbContext(CreateOptions(connectionString));

        var d1 = await ctx1.Doctors.FirstAsync(d => d.Id == doctorId);
        var d2 = await ctx2.Doctors.FirstAsync(d => d.Id == doctorId);

        d1.StopAcceptingPatients();
        await ctx1.SaveChangesAsync();

        var d2Entry = ctx2.Entry(d2);
        d2Entry.Property("RowVersion").OriginalValue = new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 };

        d2.StopAcceptingPatients();
        var act = () => ctx2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private static async Task<int> SeedDoctorAsync(string connectionString)
    {
        await using var ctx = new SqliteCompatibleDbContext(CreateOptions(connectionString));
        await ctx.Database.EnsureCreatedAsync();

        var doctor = Doctor.Create(
            "Concurrency", "Doctor",
            Email.Create("concurrency.doctor@test.com"),
            PhoneNumber.Create("+355672345678"),
            "LIC-CONCUR-DR",
            Money.Create(100m, "USD"),
            10,
            Specialty.Cardiology);

        doctor.ClearDomainEvents();
        ctx.Doctors.Add(doctor);
        await ctx.SaveChangesAsync();
        return doctor.Id;
    }

    private static async Task<SharedDatabase> CreateSharedDatabaseAsync()
    {
        var connectionString = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();
        return new SharedDatabase(keepAlive, connectionString);
    }

    private static DbContextOptions<HealthcareDbContext> CreateOptions(string connectionString)
    {
        return new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlite(connectionString)
            .Options;
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
