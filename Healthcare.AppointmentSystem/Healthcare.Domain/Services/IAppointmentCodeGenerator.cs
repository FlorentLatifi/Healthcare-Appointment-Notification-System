namespace Healthcare.Domain.Services;

/// <summary>
/// Domain Service interface for generating unique appointment reference codes.
/// </summary>
/// <remarks>
/// ╔══════════════════════════════════════════════════════════════════╗
/// ║             DESIGN PATTERN: Singleton (Creational)              ║
/// ╠══════════════════════════════════════════════════════════════════╣
/// ║  WHY this interface exists here (in Domain)?                    ║
/// ║  → Code generation is PURE BUSINESS LOGIC.                      ║
/// ║  → "APT-20260226-0001" is a domain concept, not a technical one. ║
/// ║  → The Domain layer owns the contract.                           ║
/// ║  → The implementation (Singleton) also lives in Domain.          ║
/// ╠══════════════════════════════════════════════════════════════════╣
/// ║  HEXAGONAL ARCHITECTURE placement:                               ║
/// ║  → This is a DOMAIN SERVICE INTERFACE                            ║
/// ║  → Lives in: Healthcare.Domain/Services/                         ║
/// ║  → Implementation: AppointmentCodeGenerator.cs (same folder)     ║
/// ║  → Registered in DI as: AddSingleton<IAppointmentCodeGenerator>  ║
/// ╚══════════════════════════════════════════════════════════════════╝
/// </remarks>
public interface IAppointmentCodeGenerator
{
    /// <summary>
    /// Generates a unique appointment reference code.
    /// Format: APT-YYYYMMDD-XXXX  (e.g., APT-20260226-0001)
    /// </summary>
    /// <returns>A unique, human-readable appointment code.</returns>
    string GenerateCode();

    /// <summary>
    /// Returns the total number of codes generated since application start.
    /// Useful for monitoring and diagnostics.
    /// </summary>
    int TotalGenerated { get; }
}