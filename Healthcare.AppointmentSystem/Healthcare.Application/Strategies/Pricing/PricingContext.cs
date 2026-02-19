using Healthcare.Domain.Ports.Pricing;

namespace Healthcare.Application.Strategies.Pricing;

/// <summary>
/// Context class that holds and executes the active pricing strategy.
/// </summary>
/// <remarks>
/// Design Pattern: Strategy Pattern (Behavioral) — CONTEXT role
/// 
/// The Context:
///   1. Holds a reference to the current strategy
///   2. Allows switching strategies at runtime
///   3. Delegates calculation to the strategy
///   4. Has NO knowledge of how the price is calculated
/// 
/// This is the classic Strategy Pattern structure:
///   Context → Strategy Interface → Concrete Strategy
/// 
/// Usage:
///   var context = new PricingContext(new InsurancePricingStrategy());
///   decimal price = context.ExecutePricing(100m);  // → 70.00
/// 
/// Runtime switch:
///   context.SetStrategy(new EmergencyPricingStrategy());
///   decimal price = context.ExecutePricing(100m);  // → 150.00
/// </remarks>
public sealed class PricingContext
{
    private IAppointmentPricingStrategy _strategy;

    /// <summary>
    /// Gets the name of the currently active strategy.
    /// </summary>
    public string ActiveStrategyName => _strategy.StrategyName;

    /// <summary>
    /// Initializes the context with a strategy.
    /// Default is StandardPricingStrategy.
    /// </summary>
    public PricingContext(IAppointmentPricingStrategy? strategy = null)
    {
        _strategy = strategy ?? new StandardPricingStrategy();
    }

    /// <summary>
    /// Switches the active strategy at runtime.
    /// </summary>
    /// <remarks>
    /// This is the KEY feature of Strategy Pattern:
    /// behavior changes without modifying the context.
    /// </remarks>
    public void SetStrategy(IAppointmentPricingStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        _strategy = strategy;
    }

    /// <summary>
    /// Executes the active strategy and returns the calculated price.
    /// </summary>
    /// <param name="baseFee">The doctor's base consultation fee.</param>
    /// <returns>Final price after applying the strategy.</returns>
    public decimal ExecutePricing(decimal baseFee)
    {
        if (baseFee < 0)
            throw new ArgumentException("Base fee cannot be negative.", nameof(baseFee));

        var result = _strategy.CalculatePrice(baseFee);

        Console.WriteLine(
            $"[PricingContext] Strategy: {_strategy.StrategyName} | " +
            $"Base: {baseFee:C} | Final: {result:C}");

        return result;
    }
}