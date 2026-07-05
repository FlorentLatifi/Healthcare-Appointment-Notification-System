using System.Text.Json;
using FluentAssertions;
using Healthcare.Adapters.Persistence.EntityFramework.Configurations;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Healthcare.UnitTests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.UnitTests.Adapters.Persistence.EntityFramework;

public sealed class EFCoreDoctorScheduleTests
{
    [Fact]
    public async Task DoctorSchedule_AfterCustomizingAndReloading_ShouldNotContainDuplicates()
    {
        await using var database = await CreateDatabaseAsync();
        var context = database.Context;

        var doctor = TestDataBuilder.ADoctor()
            .WithEmail("schedule.test@doctor.com")
            .WithLicense("LIC-SCHED-001")
            .Build();

        doctor.SetWorkingHours(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(13, 0));

        context.Doctors.Add(doctor);
        SetSpecialtiesJson(context, doctor);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var reloaded = await context.Doctors
            .Include(d => d.WeeklySchedule)
            .FirstOrDefaultAsync(d => d.Id == doctor.Id);

        reloaded.Should().NotBeNull();
        reloaded!.WeeklySchedule.Should().NotBeNull();

        var mondayEntries = reloaded.WeeklySchedule
            .Where(s => s.DayOfWeek == DayOfWeek.Monday)
            .ToList();

        mondayEntries.Should().HaveCount(1, because: "EF Core materialization should not duplicate schedule entries");

        var monday = mondayEntries[0];
        monday.StartTime.Should().Be(new TimeOnly(9, 0));
        monday.EndTime.Should().Be(new TimeOnly(13, 0));
        monday.IsWorkingDay.Should().BeTrue();
    }

    private static void SetSpecialtiesJson(TestDoctorDbContext context, Doctor doctor)
    {
        var specialtyInts = doctor.Specialties.Select(s => (int)s).ToList();
        var json = JsonSerializer.Serialize(specialtyInts);
        context.Entry(doctor).Property<string>("_specialtiesJson").CurrentValue = json;
    }

    private static async Task<TestDatabase> CreateDatabaseAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<TestDoctorDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new TestDoctorDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return new TestDatabase(context, connection);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public TestDatabase(TestDoctorDbContext context, SqliteConnection connection)
        {
            Context = context;
            _connection = connection;
        }

        public TestDoctorDbContext Context { get; }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class TestDoctorDbContext : DbContext
    {
        public DbSet<Doctor> Doctors => Set<Doctor>();

        public TestDoctorDbContext(DbContextOptions<TestDoctorDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfiguration(new DoctorConfiguration());
        }
    }
}
