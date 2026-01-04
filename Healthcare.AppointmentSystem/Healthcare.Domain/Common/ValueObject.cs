namespace Healthcare.Domain.Common;

/// <summary>
/// Base class for value objects following DDD principles.
/// Value objects are immutable and compared by their property values, not identity.
/// </summary>
/// <remarks>
/// Design Pattern: Value Object Pattern
/// Value objects represent descriptive aspects of the domain with no conceptual identity.
/// Two value objects are equal if all their properties are equal.
/// </remarks>
public abstract class ValueObject
{
    /// <summary>
    /// Gets the atomic values that define this value object's equality.
    /// </summary>
    /// <returns>An enumerable of values used for equality comparison.</returns>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    /// <summary>
    /// Determines whether the specified object is equal to the current value object.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
        {
            return false;
        }

        var other = (ValueObject)obj;

        return GetEqualityComponents()
            .SequenceEqual(other.GetEqualityComponents());
    }

    /// <summary>
    /// Returns a hash code for the current value object.
    /// </summary>
    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Where(x => x is not null)
            .Aggregate(1, (current, obj) =>
            {
                unchecked
                {
                    return (current * 23) + obj!.GetHashCode();
                }
            });
    }

    /// <summary>
    /// Determines whether two value objects are equal.
    /// </summary>
    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        if (left is null && right is null)
            return true;

        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two value objects are not equal.
    /// </summary>
    public static bool operator !=(ValueObject? left, ValueObject? right)
    {
        return !(left == right);
    }
}