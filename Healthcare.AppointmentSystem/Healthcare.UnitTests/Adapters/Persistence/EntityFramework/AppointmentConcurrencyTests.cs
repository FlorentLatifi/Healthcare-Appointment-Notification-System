using FluentAssertions;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Healthcare.Domain.Services;
using Healthcare.UnitTests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Healthcare.UnitTests.Adapters.Persistence.EntityFramework;

public sealed class SqliteCompatibleDbContext : HealthcareDbContext
{
    public SqliteCompatibleDbContext(DbContextOptions<HealthcareDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Override column types for SQLite compatibility
        modelBuilder.Entity<AuditLogEntry>()
            .Property(a => a.Details)
            .HasColumnType("TEXT");

        // SQLite does not support IsRowVersion() semantics — the temporary
        // value generator used by IsRowVersion() does not persist values
        // to the database, so the concurrency token never changes.
        // Override to IsConcurrencyToken() without auto-generation and
        // drive the conflict manually in the test.
        modelBuilder.Entity<Appointment>()
            .Property<byte[]>("RowVersion")
            .ValueGeneratedNever()
            .IsConcurrencyToken();
    }
}

public sealed class AppointmentConcurrencyTests
{
    [Fact]
    public async Task SaveChangesAsync_WithConcurrentUpdates_ThrowsDbUpdateConcurrencyException()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlite(connection)
            .Options;

        var doctor = TestDataBuilder.ADoctor()
            .WithLicense("CON-10001")
            .WithEmail("concurrency.doctor@test.com")
            .Build();
        var patient = TestDataBuilder.APatient()
            .WithEmail("concurrency.patient@test.com")
            .Build();
        var appointmentTime = AppointmentTime.Create(DateTime.UtcNow.Date.AddDays(30).AddHours(10));

        int appointmentId;
        await using (var seedCtx = new SqliteCompatibleDbContext(options))
        {
            await seedCtx.Database.EnsureCreatedAsync();
            seedCtx.Doctors.Add(doctor);
            seedCtx.Patients.Add(patient);
            seedCtx.Entry(doctor).Property<string>("_specialtiesJson").CurrentValue =
                System.Text.Json.JsonSerializer.Serialize(
                    new List<int> { (int)Specialty.GeneralPractice });
            await seedCtx.SaveChangesAsync();

            var appointment = Appointment.Create(patient, doctor, appointmentTime,
                "Concurrency test appointment for optimistic locking verification",
                AppointmentCodeGenerator.Instance);
            seedCtx.Appointments.Add(appointment);
            await seedCtx.SaveChangesAsync();
            appointmentId = appointment.Id;
        }

        await using var ctx1 = new SqliteCompatibleDbContext(options);
        await using var ctx2 = new SqliteCompatibleDbContext(options);

        var a1 = await ctx1.Appointments.FirstAsync(a => a.Id == appointmentId);
        var a2 = await ctx2.Appointments.FirstAsync(a => a.Id == appointmentId);

        a1.ApplyPricingStrategy(75, "USD");

        // Manually advance the concurrency token so ctx1's save changes the
        // database value.  SQLite's TemporaryBinaryValueGenerator does not
        // persist generated values, so we drive the token ourselves.
        ctx1.Entry(a1).Property<byte[]>("RowVersion").CurrentValue =
            Guid.NewGuid().ToByteArray();
        await ctx1.SaveChangesAsync();

        a2.ApplyPricingStrategy(80, "USD");
        // ctx2 still tracks the *original* RowVersion; the WHERE clause on
        // its UPDATE will match zero rows → DbUpdateConcurrencyException.
        var act = () => ctx2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
