namespace Healthcare.Domain.Common;

/// <summary>
/// Marker interface for domain events following the Observer pattern.
/// Domain events represent something that happened in the domain that domain experts care about.
/// </summary>
/// <remarks>
/// Design Pattern: Observer Pattern (foundation)
/// Events are published when important state changes occur in aggregates.
/// Multiple handlers can observe and react to the same event.
/// </remarks>
public interface IDomainEvent
{
    /// <summary>
    /// Gets the unique identifier for this domain event.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// Gets the date and time when the event occurred.
    /// </summary>
    DateTime OccurredOn { get; }
}
