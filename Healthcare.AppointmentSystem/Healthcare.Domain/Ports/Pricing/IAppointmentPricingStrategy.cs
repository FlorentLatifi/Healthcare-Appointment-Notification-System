namespace Healthcare.Domain.Ports.Pricing;

/// <summary>
/// Strategy interface for calculating appointment pricing.
/// </summary>
/// <remarks>
/// Design Pattern: Strategy Pattern (Behavioral)
/// 
/// WHY: Different appointment types require different pricing rules.
///   - Standard consultation → base fee
///   - Insurance patient     → discounted fee
///   - Emergency             → premium fee
/// 
/// WHERE (Hexagonal Architecture):
///   This is a PORT inside the Domain — the core defines WHAT it needs.
///   The concrete strategies (ADAPTERS) live in Application or Adapters layer.
/// 
/// HOW IT WORKS:
///   1. Application layer selects the correct strategy at runtime
///   2. Calls CalculatePrice(baseFee) → returns final price
///   3. Domain stays unaware of which strategy is active
/// </remarks>
public interface IAppointmentPricingStrategy
{
    /// <summary>
    /// The name of this pricing strategy (for logging/debugging).
    /// </summary>
    string StrategyName { get; }

    /// <summary>
    /// Calculates the final price based on the doctor's base fee.
    /// </summary>
    /// <param name="baseFee">The doctor's standard consultation fee.</param>
    /// <returns>The final calculated price.</returns>
    decimal CalculatePrice(decimal baseFee);
}