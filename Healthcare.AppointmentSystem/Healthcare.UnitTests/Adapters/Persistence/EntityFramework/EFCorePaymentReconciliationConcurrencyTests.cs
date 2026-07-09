using FluentAssertions;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Adapters.Persistence.EntityFramework.Repositories;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Services;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Adapters.Services;
using Healthcare.Domain.ValueObjects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Healthcare.UnitTests.Adapters.Persistence.EntityFramework;

[Trait("Category", "Integration")]
public sealed class EFCorePaymentReconciliationConcurrencyTests
{
    [Fact]
    public async Task ReconcilePaymentAsync_TwoConcurrentCalls_ResultsInOnePaymentRow()
    {
        await using var sharedDb = await CreateSharedDatabaseAsync();
        var connectionString = sharedDb.ConnectionString;

        var (appointmentId, _) = await SeedDataAsync(connectionString);

        var (service1, ctx1) = CreateService(connectionString);
        var (service2, ctx2) = CreateService(connectionString);

        var task1 = Task.Run(() => service1.ReconcilePaymentAsync(
            appointmentId, "pi_concur_1", true, "tx_concur_1", "card", null));

        var task2 = Task.Run(() => service2.ReconcilePaymentAsync(
            appointmentId, "pi_concur_2", true, "tx_concur_2", "card", null));

        await Task.WhenAll(task1, task2);

        await ctx1.DisposeAsync();
        await ctx2.DisposeAsync();

        await using var verifyCtx = CreateDbContext(connectionString);
        var payments = await verifyCtx.Payments
            .Where(p => p.AppointmentId == appointmentId)
            .ToListAsync();

        payments.Should().HaveCount(1);
        payments[0].Status.Should().Be(PaymentStatus.Succeeded);
    }

    private static async Task<(int AppointmentId, int DoctorId)> SeedDataAsync(string connectionString)
    {
        await using var ctx = CreateDbContext(connectionString);
        await ctx.Database.EnsureCreatedAsync();

        var doctor = Doctor.Create(
            "Jane", "Smith",
            Email.Create("concur.test@doctor.com"),
            PhoneNumber.Create("+38349987654"),
            "LIC-CONCUR-001",
            Money.Create(50, "USD"),
            10,
            Specialty.GeneralPractice);

        var patient = Patient.Create(
            "John", "Doe",
            Email.Create("concur.test@patient.com"),
            PhoneNumber.Create("+38349123456"),
            new DateTime(1990, 1, 1),
            Gender.Male,
            Address.Create("Main St", "Pristina", "Kosovo", "10000", "Kosovo"));

        var scheduledTime = AppointmentTime.Create(
            DateTime.UtcNow.AddDays(7).Date.AddHours(10));

        var appointment = Appointment.Create(
            patient, doctor, scheduledTime,
            "Concurrency test appointment",
            AppointmentCodeGenerator.Instance);

        appointment.ClearDomainEvents();

        ctx.Doctors.Add(doctor);
        ctx.Patients.Add(patient);
        ctx.Appointments.Add(appointment);
        await ctx.SaveChangesAsync();

        return (appointment.Id, doctor.Id);
    }

    private static async Task<SharedDatabase> CreateSharedDatabaseAsync()
    {
        var connectionString = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();
        return new SharedDatabase(keepAlive, connectionString);
    }

    private static HealthcareDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new HealthcareDbContext(options);
    }

    private static (PaymentReconciliationService Service, HealthcareDbContext Context) CreateService(
        string connectionString)
    {
        var ctx = CreateDbContext(connectionString);
        var uow = new EFCoreUnitOfWork(
            ctx,
            new EFCoreAppointmentRepository(ctx),
            new EFCorePatientRepository(ctx),
            new EFCoreDoctorRepository(ctx),
            new EFCoreUserRepository(ctx),
            new EFCorePaymentRepository(ctx),
            new EFCoreAuditLogRepository(ctx),
            new EFCoreUserSessionRepository(ctx));

        var eventDispatcher = Mock.Of<IDomainEventDispatcher>();
        var logger = Mock.Of<ILogger<PaymentReconciliationService>>();
        var service = new PaymentReconciliationService(uow, eventDispatcher, logger);

        return (service, ctx);
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
