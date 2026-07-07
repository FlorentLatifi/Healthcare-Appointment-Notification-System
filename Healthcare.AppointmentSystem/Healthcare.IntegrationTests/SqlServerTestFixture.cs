using Healthcare.Adapters.Persistence.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Healthcare.IntegrationTests;

public sealed class SqlServerTestFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container;

    public SqlServerTestFixture()
    {
        _container = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("YourStrong!Passw0rd")
            .Build();
    }

    public DbContextOptions<HealthcareDbContext> CreateDbContextOptions()
    {
        return new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await RunMigrationsAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    private async Task RunMigrationsAsync()
    {
        var options = CreateDbContextOptions();
        await using var context = new HealthcareDbContext(options);
        await context.Database.MigrateAsync();
    }
}
