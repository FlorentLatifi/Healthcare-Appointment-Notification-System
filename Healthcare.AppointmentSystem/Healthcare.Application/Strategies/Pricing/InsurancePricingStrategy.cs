using Healthcare.Domain.Ports.Pricing;

namespace Healthcare.Application.Strategies.Pricing;

/// <summary>
/// Insurance pricing — patient pays a reduced fee (insurance covers the rest).
/// </summary>
/// <remarks>
/// Design Pattern: Strategy Pattern (Behavioral)
/// 
/// The discount percentage is configurable via constructor.
/// Default: 30% discount (insurance covers 30% of the base fee).
/// 
/// Example:
///   Base fee = $100
///   Discount = 30%
///   Patient pays = $70
/// 
/// Used when:
///   - Patient has valid health insurance
///   - Insurance has been validated externally
/// </remarks>
public sealed class InsurancePricingStrategy : IAppointmentPricingStrategy
{
    private readonly decimal _discountPercentage;

    public string StrategyName => $"Insurance (discount: {_discountPercentage}%)";

    /// <summary>
    /// Creates an insurance pricing strategy.
    /// </summary>
    /// <param name="discountPercentage">
    /// The discount percentage (0-100). Default is 30.
    /// </param>
    public InsurancePricingStrategy(decimal discountPercentage = 30m)
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