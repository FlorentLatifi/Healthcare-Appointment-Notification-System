using Healthcare.Application.Ports.Repositories;

namespace Healthcare.Application.Ports.Factories;

/// <summary>
/// Abstract Factory for creating repository instances.
/// </summary>
/// <remarks>
/// Design Pattern: Abstract Factory (Creational)
/// 
/// WHY Abstract Factory and not just DI?
///   - DI registers ONE implementation globally.
///   - Abstract Factory allows creating FAMILIES of related objects
///     that must work together consistently.
///   - Makes it explicit that InMemory repos belong together,
///     and EFCore repos belong together.
///   - Switching persistence = swap one factory, not 3 DI registrations.
/// 
/// WHERE (Hexagonal Architecture):
///   This is a PORT in the Application layer.
///   Concrete factories (ADAPTERS) live in Healthcare.Adapters.
/// 
/// FAMILY 1 — InMemory (development/testing):
///   CreateAppointmentRepository() → InMemoryAppointmentRepository
///   CreatePatientRepository()     → InMemoryPatientRepository
///   CreateDoctorRepository()      → InMemoryDoctorRepository
/// 
/// FAMILY 2 — EFCore (production):
///   CreateAppointmentRepository() → EFCoreAppointmentRepository
///   CreatePatientRepository()     → EFCorePatientRepository
///   CreateDoctorRepository()      → EFCoreDoctorRepository
/// </remarks>
public interface IHealthcareRepositoryFactory
{
    /// <summary>
    /// Gets the name of this factory (for logging/debugging).
    /// </summary>
    string FactoryName { get; }

    /// <summary>
    /// Creates an appointment repository.
    /// </summary>
    IAppointmentRepository CreateAppointmentRepository();

    /// <summary>
    /// Creates a patient repository.
    /// </summary>
    IPatientRepository CreatePatientRepository();

    /// <summary>
    /// Creates a doctor repository.
    /// </summary>
    IDoctorRepository CreateDoctorRepository();
}
