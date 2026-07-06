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

    [Fact]
    public async Task DoctorSpecialties_RoundTripsCorrectly()
    {
        await using var database = await CreateDatabaseAsync();
        var context = database.Context;

        var doctor = TestDataBuilder.ADoctor()
            .WithEmail("specialties.test@doctor.com")
            .WithLicense("LIC-SPEC-001")
            .WithSpecialty(Specialty.Cardiology)
            .Build();

        context.Doctors.Add(doctor);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var reloaded = await context.Doctors
            .Include(d => d.SpecialtyEntries)
            .FirstOrDefaultAsync(d => d.Id == doctor.Id);

        reloaded.Should().NotBeNull();
        reloaded!.Specialties.Should().HaveCount(1);
        reloaded.Specialties.Should().Contain(Specialty.Cardiology);
    }

    [Fact]
    public async Task DoctorSpecialties_MultipleSpecialtiesRoundTripCorrectly()
    {
        await using var database = await CreateDatabaseAsync();
        var context = database.Context;

        var doctor = TestDataBuilder.ADoctor()
            .WithEmail("multi.specialties@doctor.com")
            .WithLicense("LIC-MULTI-001")
            .WithSpecialty(Specialty.GeneralPractice)
            .Build();

        doctor.AddSpecialty(Specialty.Cardiology);

        context.Doctors.Add(doctor);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var reloaded = await context.Doctors
            .Include(d => d.SpecialtyEntries)
            .FirstOrDefaultAsync(d => d.Id == doctor.Id);

        reloaded.Should().NotBeNull();
        reloaded!.Specialties.Should().HaveCount(2);
        reloaded.Specialties.Should().Contain(Specialty.GeneralPractice);
        reloaded.Specialties.Should().Contain(Specialty.Cardiology);
    }

    [Fact]
    public async Task GetBySpecialtyAsync_ReturnsOnlyMatchingDoctors()
    {
        await using var database = await CreateDatabaseAsync();
        var context = database.Context;

        var cardiologist = TestDataBuilder.ADoctor()
            .WithEmail("cardio@doctor.com")
            .WithLicense("LIC-CARDIO-001")
            .WithSpecialty(Specialty.Cardiology)
            .Build();

        var gp = TestDataBuilder.ADoctor()
            .WithEmail("gp@doctor.com")
            .WithLicense("LIC-GP-001")
            .WithSpecialty(Specialty.GeneralPractice)
            .Build();

        context.Doctors.AddRange(cardiologist, gp);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var cardioResults = await context.Doctors
            .Include(d => d.SpecialtyEntries)
            .Where(d => d.SpecialtyEntries.Any(e => e.Specialty == Specialty.Cardiology))
            .AsNoTracking()
            .ToListAsync();

        cardioResults.Should().HaveCount(1);
        cardioResults[0].Email.Value.Should().Be("cardio@doctor.com");
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
