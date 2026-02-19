using Healthcare.Domain.Ports.Pricing;

namespace Healthcare.Application.Strategies.Pricing;

/// <summary>
/// Emergency pricing — patient pays a premium on top of the base fee.
/// </summary>
/// <remarks>
/// Design Pattern: Strategy Pattern (Behavioral)
/// 
/// The premium percentage is configurable via constructor.
/// Default: 50% premium (emergency appointments cost 50% more).
/// 
/// Example:
///   Base fee = $100
///   Premium  = 50%
///   Patient pays = $150
/// 
/// Used when:
///   - Appointment is marked as emergency
///   - Outside regular office hours
/// </remarks>
public sealed class EmergencyPricingStrategy : IAppointmentPricingStrategy
{
    private readonly decimal _premiumPercentage;

    public string StrategyName => $"Emergency (premium: {_premiumPercentage}%)";

    /// <summary>
    /// Creates an emergency pricing strategy.
    /// </summary>
    /// <param name="premiumPercentage">
    /// The premium percentage (0-200). Default is 50.
    /// </param>
    public EmergencyPricingStrategy(decimal premiumPercentage = 50m)
    {
        if (premiumPercentage < 0 || premiumPercentage > 200)
            throw new ArgumentOutOfRangeException(
                nameof(premiumPercentage),
                "Premium must be between 0 and 200.");

        _premiumPercentage = premiumPercentage;
    }

    public decimal CalculatePrice(decimal baseFee)
    {
        var premium = baseFee * (_premiumPercentage / 100m);
        return Math.Round(baseFee + premium, 2);
    }
}