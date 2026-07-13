using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Adapters.Persistence.EntityFramework.Repositories;
using Healthcare.Application.Ports.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.IntegrationTests.Helpers;

/// <summary>
/// Shared in-memory SQLite for tests that need real EF Core identity generation
/// (auto-increment keys after <c>SaveChangesAsync</c>). Used by profile-link identity
/// regression tests in this project so CI always executes them (no Testcontainers required).
/// </summary>
public sealed class EfCoreSqliteFixture : IAsyncDisposable
{
    private readonly SqliteConnection _keepAlive;

    private EfCoreSqliteFixture(SqliteConnection keepAlive, string connectionString)
    {
        _keepAlive = keepAlive;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static async Task<EfCoreSqliteFixture> CreateAsync()
    {
        var connectionString = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();
        return new EfCoreSqliteFixture(keepAlive, connectionString);
    }

    public DbContextOptions<HealthcareDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlite(ConnectionString)
            .Options;

    public SqliteCompatibleDbContext CreateContext() =>
        new(CreateOptions());

    public IUnitOfWork CreateUnitOfWork(HealthcareDbContext context) =>
        new EFCoreUnitOfWork(
            context,
            new EFCoreAppointmentRepository(context),
            new EFCorePatientRepository(context),
            new EFCoreDoctorRepository(context),
            new EFCoreUserRepository(context),
            new EFCorePaymentRepository(context),
            new EFCoreAuditLogRepository(context),
            new EFCoreUserSessionRepository(context));

    public async ValueTask DisposeAsync()
    {
        await _keepAlive.DisposeAsync();
    }
}
