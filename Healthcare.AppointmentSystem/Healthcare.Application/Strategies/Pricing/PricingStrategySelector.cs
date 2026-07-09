using Healthcare.Domain.Enums;
using Healthcare.Domain.Ports.Pricing;

namespace Healthcare.Application.Strategies.Pricing;

/// <summary>
/// Selects the correct pricing strategy based on appointment type.
/// </summary>
/// <remarks>
/// 
/// This selector acts as a simple factory for strategies.
/// It centralizes the decision of WHICH strategy to use,
/// keeping BookAppointmentHandler clean and focused.
/// 
/// Adding a new pricing rule = add a new strategy + one line here.
/// No existing code is modified (Open/Closed Principle).
/// </remarks>
public static class PricingStrategySelector
{
    /// <summary>
    /// Returns the appropriate pricing strategy for the given appointment type.
    /// </summary>
    public static IAppointmentPricingStrategy Select(AppointmentType type)
    {
        return type switch
        {
            AppointmentType.Standard => new StandardPricingStrategy(),
            AppointmentType.Insurance => new InsurancePricingStrategy(discountPercentage: 30m),
            AppointmentType.Emergency => new EmergencyPricingStrategy(premiumPercentage: 50m),
            AppointmentType.Vip => new VipPricingStrategy(discountPercentage: 20m),
            _ => new StandardPricingStrategy()
        };
    }
}