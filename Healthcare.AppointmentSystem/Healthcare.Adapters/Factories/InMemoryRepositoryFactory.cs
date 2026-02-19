using Healthcare.Application.Ports.Factories;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Adapters.Persistence.InMemory;

namespace Healthcare.Adapters.Factories;

/// <summary>
/// Concrete Factory that creates IN-MEMORY repository instances.
/// </summary>
/// <remarks>
/// Design Pattern: Abstract Factory (Creational) — Concrete Factory
/// 
/// USE THIS FOR:
///   - Local development
///   - Unit and integration tests
///   - Demos and prototypes
/// 
/// All repositories share the same in-memory data store
/// (via singleton registration in DI).
/// 
/// Switching to production = replace with EFCoreRepositoryFactory.
/// NO other code changes needed.
/// </remarks>
public sealed class InMemoryRepositoryFactory : IHealthcareRepositoryFactory
{
    public string FactoryName => "InMemory";

    public IAppointmentRepository CreateAppointmentRepository()
    {
        Console.WriteLine(
            "[InMemoryRepositoryFactory] Creating InMemoryAppointmentRepository");
        return new InMemoryAppointmentRepository();
    }

    public IPatientRepository CreatePatientRepository()
    {
        Console.WriteLine(
            "[InMemoryRepositoryFactory] Creating InMemoryPatientRepository");
        return new InMemoryPatientRepository();
    }

    public IDoctorRepository CreateDoctorRepository()
    {
        Console.WriteLine(
            "[InMemoryRepositoryFactory] Creating InMemoryDoctorRepository");
        return new InMemoryDoctorRepository();
    }
}
