using Healthcare.Domain.Ports.Pricing;

namespace Healthcare.Application.Strategies.Pricing;

/// <summary>
/// VIP pricing — loyal patients receive a special discount.
/// </summary>
/// <remarks>
/// 
/// Default: 20% discount for VIP patients.
/// 
/// Example:
///   Base fee = $100
///   Discount = 20%
///   Patient pays = $80
/// 
/// Used when:
///   - Patient is flagged as VIP in the system
///   - Patient has completed 10+ appointments
/// </remarks>
public sealed class VipPricingStrategy : IAppointmentPricingStrategy
{
    private readonly decimal _discountPercentage;

    public string StrategyName => $"VIP (discount: {_discountPercentage}%)";

    public VipPricingStrategy(decimal discountPercentage = 20m)
    {
        if (discountPercentage < 0 || discountPercentage > 100)
            throw new ArgumentOutOfRangeException(
                nameof(discountPercentage),
                "Discount must be between 0 and 100.");

        _discountPercentage = discountPercentage;
    }

    public decimal CalculatePrice(decimal baseFee)
    {
        var discount = baseFee * (_discountPercentage / 100m);
        return Math.Round(baseFee - discount, 2);
    }
}