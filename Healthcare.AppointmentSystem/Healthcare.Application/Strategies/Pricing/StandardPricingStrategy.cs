using Healthcare.Domain.Ports.Pricing;

namespace Healthcare.Application.Strategies.Pricing;

/// <summary>
/// Standard pricing — patient pays the doctor's full base fee.
/// </summary>
/// <remarks>
/// Design Pattern: Strategy Pattern (Behavioral)
/// 
/// This is the DEFAULT strategy.
/// No discounts, no premiums — just the base consultation fee.
/// 
/// Used when:
///   - Patient has no insurance
///   - Appointment is a regular consultation
/// </remarks>
public sealed class StandardPricingStrategy : IAppointmentPricingStrategy
{
    public string StrategyName => "Standard";

    public decimal CalculatePrice(decimal baseFee)
    {
        // No modification — full price
        return baseFee;
    }
}