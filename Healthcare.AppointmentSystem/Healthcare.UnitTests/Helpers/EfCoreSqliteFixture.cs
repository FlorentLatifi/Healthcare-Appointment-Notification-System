using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Adapters.Persistence.EntityFramework.Repositories;
using Healthcare.Application.Ports.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.UnitTests.Helpers;

/// <summary>
/// Shared in-memory SQLite database for tests that need <strong>real EF Core identity generation</strong>
/// (auto-increment keys after <c>SaveChangesAsync</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists:</b> Moq-based repository tests never assign database-generated IDs.
/// Handlers that call <c>entity.Id</c> before <c>SaveChangesAsync</c> look correct under Moq
/// (Id stays 0 or a fake value) but fail against SQL Server / SQLite identity columns.
/// The profile-linking bug (User.PatientId / User.DoctorId = 0) was invisible until real EF was used.
/// </para>
/// <para>
/// <b>When to use:</b>
/// <list type="bullet">
/// <item>Any handler that INSERTs an entity, then reads its <c>Id</c> to update another aggregate</item>
/// <item>Unique indexes / concurrency tokens that depend on real persistence</item>
/// <item>Propagation of FKs or scalar “link” properties after identity assignment</item>
/// </list>
/// Prefer Moq for pure domain / validation paths that never touch generated keys.
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// await using var db = await EfCoreSqliteFixture.CreateAsync();
/// await using var ctx = db.CreateContext();
/// await ctx.Database.EnsureCreatedAsync();
/// var uow = db.CreateUnitOfWork(ctx);
/// // ... act ...
/// await using var verify = db.CreateContext(); // fresh context = true DB state
/// </code>
/// </para>
/// See also <c>Helpers/README.md</c> (EF Core identity testing).
/// </remarks>
public sealed class EfCoreSqliteFixture : IAsyncDisposable
{
    private readonly SqliteConnection _keepAlive;

    private EfCoreSqliteFixture(SqliteConnection keepAlive, string connectionString)
    {
        _keepAlive = keepAlive;
        ConnectionString = connectionString;
    }

    /// <summary>Shared-cache in-memory connection string (unique per fixture instance).</summary>
    public string ConnectionString { get; }

    /// <summary>
    /// Creates a new isolated in-memory database. The keep-alive connection stays open until dispose
    /// so SQLite does not drop the shared memory database between contexts.
    /// </summary>
    public static async Task<EfCoreSqliteFixture> CreateAsync()
    {
        var connectionString = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();
        return new EfCoreSqliteFixture(keepAlive, connectionString);
    }

    /// <summary>Builds <see cref="DbContextOptions{TContext}"/> for SQLite against this fixture.</summary>
    public DbContextOptions<HealthcareDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlite(ConnectionString)
            .Options;

    /// <summary>
    /// New <see cref="SqliteCompatibleDbContext"/> (RowVersion / TEXT tweaks for SQLite).
    /// Call <c>EnsureCreatedAsync</c> once after the first context is created.
    /// </summary>
    public SqliteCompatibleDbContext CreateContext() =>
        new(CreateOptions());

    /// <summary>
    /// Real <see cref="EFCoreUnitOfWork"/> wired to the same context (not mocks).
    /// Use the same <paramref name="context"/> instance the handler will use for the act phase.
    /// </summary>
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
