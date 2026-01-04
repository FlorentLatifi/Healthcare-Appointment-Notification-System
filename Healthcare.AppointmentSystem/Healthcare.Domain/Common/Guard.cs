namespace Healthcare.Domain.Common;

/// <summary>
/// Provides guard clause methods for parameter validation.
/// </summary>
/// <remarks>
/// Guard clauses help fail fast and make preconditions explicit.
/// This is a defensive programming technique to ensure data integrity.
/// </remarks>
public static class Guard
{
    /// <summary>
    /// Ensures that the specified string is not null or whitespace.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <exception cref="ArgumentException">Thrown when value is null or whitespace.</exception>
    public static void AgainstNullOrWhiteSpace(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"{parameterName} cannot be null or whitespace.",
                parameterName);
        }
    }

    /// <summary>
    /// Ensures that the specified value is not null.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
    public static void AgainstNull<T>(T? value, string parameterName) where T : class
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }
    }

    /// <summary>
    /// Ensures that the specified number is positive.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <exception cref="ArgumentException">Thrown when value is not positive.</exception>
    public static void AgainstNegativeOrZero(decimal value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentException(
                $"{parameterName} must be positive.",
                parameterName);
        }
    }

    /// <summary>
    /// Ensures that the specified date is in the future.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <exception cref="ArgumentException">Thrown when date is not in the future.</exception>
    public static void AgainstPastDate(DateTime value, string parameterName)
    {
        if (value <= DateTime.UtcNow)
        {
            throw new ArgumentException(
                $"{parameterName} must be in the future.",
                parameterName);
        }
    }
}