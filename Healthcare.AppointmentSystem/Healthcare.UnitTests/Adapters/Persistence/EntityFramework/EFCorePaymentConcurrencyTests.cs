using FluentAssertions;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Adapters.Services;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Healthcare.UnitTests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.UnitTests.Adapters.Persistence.EntityFramework;

[Trait("Category", "Integration")]
public sealed class EFCorePaymentConcurrencyTests
{
    [Fact]
    public async Task SaveChangesAsync_ConcurrentPaymentUpdates_ThrowsDbUpdateConcurrencyException()
    {
        await using var sharedDb = await CreateSharedDatabaseAsync();
        var connectionString = sharedDb.ConnectionString;

        var paymentId = await SeedPaymentAsync(connectionString);

        await using var ctx1 = new SqliteCompatibleDbContext(CreateOptions(connectionString));
        await using var ctx2 = new SqliteCompatibleDbContext(CreateOptions(connectionString));

        var p1 = await ctx1.Payments.FirstAsync(p => p.Id == paymentId);
        var p2 = await ctx2.Payments.FirstAsync(p => p.Id == paymentId);

        p1.MarkAsSucceeded(
            TransactionId.Create("tx_concur_1"),
            "card");
        await ctx1.SaveChangesAsync();

        var p2Entry = ctx2.Entry(p2);
        p2Entry.Property("RowVersion").OriginalValue = new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 };

        p2.MarkAsSucceeded(
            TransactionId.Create("tx_concur_2"),
            "card");
        var act = () => ctx2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private static async Task<int> SeedPaymentAsync(string connectionString)
    {
        await using var ctx = new SqliteCompatibleDbContext(CreateOptions(connectionString));
        await ctx.Database.EnsureCreatedAsync();

        var doctor = Doctor.Create(
            "Payment", "Concurrency",
            Email.Create("payment.concurrency@doctor.com"),
            PhoneNumber.Create("+355672345678"),
            "LIC-CONCUR-PAY",
            Money.Create(100m, "USD"),
            10,
            Specialty.Cardiology);

        var patient = Patient.Create(
            "Payment", "Concurrency",
            Email.Create("payment.concurrency@patient.com"),
            PhoneNumber.Create("+355672345678"),
            new DateTime(1990, 1, 1),
            Gender.Male,
            Address.Create("1 Test St", "Pristina", "Kosovo", "10000", "Kosovo"));

        var scheduledTime = AppointmentTime.Create(
            DateTime.UtcNow.AddDays(7).Date.AddHours(10));

        var appointment = Appointment.Create(
            patient, doctor, scheduledTime,
            "Payment concurrency test",
            new AppointmentCodeGenerator());

        appointment.ClearDomainEvents();
        doctor.ClearDomainEvents();

        ctx.Doctors.Add(doctor);
        ctx.Patients.Add(patient);
        ctx.Appointments.Add(appointment);
        await ctx.SaveChangesAsync();

        var payment = Payment.Create(appointment.Id, Money.Create(100m, "USD"));
        payment.ClearDomainEvents();
        ctx.Payments.Add(payment);
        await ctx.SaveChangesAsync();

        return payment.Id;
    }

    private static async Task<EfCoreSqliteFixture> CreateSharedDatabaseAsync() =>
        await EfCoreSqliteFixture.CreateAsync();

    private static DbContextOptions<HealthcareDbContext> CreateOptions(string connectionString) =>
        new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlite(connectionString)
            .Options;
}
