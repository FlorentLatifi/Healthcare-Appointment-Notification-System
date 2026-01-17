using Healthcare.Domain.Common;

namespace Healthcare.Domain.ValueObjects;

/// <summary>
/// Represents a payment gateway transaction ID (e.g., Stripe Payment Intent ID).
/// </summary>
/// <remarks>
/// Design Pattern: Value Object Pattern
/// 
/// Ensures transaction IDs are always valid and consistent.
/// Example: "pi_3QK5ZB2eZvKYlo2C0X8Z5X6Y" (Stripe format)
/// </remarks>
public sealed class TransactionId : ValueObject
{
    /// <summary>
    /// Gets the transaction identifier from the payment gateway.
    /// </summary>
    public string Value { get; }

    private TransactionId(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new TransactionId with validation.
    /// </summary>
    public static TransactionId Create(string transactionId)
    {
        Guard.AgainstNullOrWhiteSpace(transactionId, nameof(transactionId));

        var normalized = transactionId.Trim();

        if (normalized.Length < 10)
        {
            throw new ArgumentException(
                "Transaction ID must be at least 10 characters.",
                nameof(transactionId));
        }

        if (normalized.Length > 255)
        {
            throw new ArgumentException(
                "Transaction ID cannot exceed 255 characters.",
                nameof(transactionId));
        }

        return new TransactionId(normalized);
    }

    public override string ToString() => Value;

    public static implicit operator string(TransactionId transactionId) => transactionId.Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}