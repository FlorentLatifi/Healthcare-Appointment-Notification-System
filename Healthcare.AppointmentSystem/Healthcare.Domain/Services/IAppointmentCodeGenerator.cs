namespace Healthcare.Domain.Services;

/// <summary>
/// Domain Service interface for generating unique appointment reference codes.
/// </summary>
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