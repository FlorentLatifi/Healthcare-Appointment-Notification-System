using Healthcare.Application.Ports.Factories;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Adapters.Persistence.EntityFramework.Repositories;

namespace Healthcare.Adapters.Factories;

/// <summary>
/// Concrete Factory that creates EF CORE repository instances.
/// </summary>
/// <remarks>
/// Design Pattern: Abstract Factory (Creational) — Concrete Factory
/// 
/// USE THIS FOR:
///   - Production environment
///   - SQL Server / PostgreSQL
/// 
/// All repositories share the same DbContext instance
/// to ensure Unit of Work consistency.
/// 
/// Switching from InMemory = replace InMemoryRepositoryFactory
/// with this. NO business logic changes needed.
/// </remarks>
public sealed class EFCoreRepositoryFactory : IHealthcareRepositoryFactory
{
    private readonly HealthcareDbContext _context;

    public string FactoryName => "EFCore";

    public EFCoreRepositoryFactory(HealthcareDbContext context)
    {
        _context = context;
    }

    public IAppointmentRepository CreateAppointmentRepository()
    {
        Console.WriteLine(
            "[EFCoreRepositoryFactory] Creating EFCoreAppointmentRepository");
        return new EFCoreAppointmentRepository(_context);
    }

    public IPatientRepository CreatePatientRepository()
    {
        Console.WriteLine(
            "[EFCoreRepositoryFactory] Creating EFCorePatientRepository");
        return new EFCorePatientRepository(_context);
    }

    public IDoctorRepository CreateDoctorRepository()
    {
        Console.WriteLine(
            "[EFCoreRepositoryFactory] Creating EFCoreDoctorRepository");
        return new EFCoreDoctorRepository(_context);
    }
}