using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Healthcare.Adapters.Persistence.EntityFramework;

public class HealthcareDbContextFactory : IDesignTimeDbContextFactory<HealthcareDbContext>
{
    public HealthcareDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HealthcareDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=HealthcareDb_SyncTemp;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true",
            sql => sql.MigrationsAssembly(typeof(HealthcareDbContextFactory).Assembly.FullName));

        return new HealthcareDbContext(optionsBuilder.Options);
    }
}
