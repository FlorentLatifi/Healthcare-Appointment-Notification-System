using Healthcare.Application.Ports.Factories;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Adapters.Persistence.InMemory;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<InMemoryRepositoryFactory> _logger;

    public string FactoryName => "InMemory";

    public InMemoryRepositoryFactory(ILogger<InMemoryRepositoryFactory> logger)
    {
        _logger = logger;
    }

    public IAppointmentRepository CreateAppointmentRepository()
    {
        _logger.LogDebug("[InMemoryRepositoryFactory] Creating InMemoryAppointmentRepository");
        return new InMemoryAppointmentRepository();
    }

    public IPatientRepository CreatePatientRepository()
    {
        _logger.LogDebug("[InMemoryRepositoryFactory] Creating InMemoryPatientRepository");
        return new InMemoryPatientRepository();
    }

    public IDoctorRepository CreateDoctorRepository()
    {
        _logger.LogDebug("[InMemoryRepositoryFactory] Creating InMemoryDoctorRepository");
        return new InMemoryDoctorRepository();
    }
}
