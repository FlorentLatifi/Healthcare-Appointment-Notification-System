namespace Healthcare.Domain.Common;

/// <summary>
/// Base class for all domain entities.
/// Entities have a unique identity and their equality is based on their ID, not their properties.
/// </summary>
/// <remarks>
/// Design Pattern: Domain Model Pattern + Observer Pattern (via domain events)
/// Entities represent objects with a distinct identity that runs through time and different states.
/// </remarks>
public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>
    /// Gets the unique identifier for this entity.
    /// </summary>
    public int Id { get; protected set; }

    /// <summary>
    /// Gets the date and time when this entity was created.
    /// </summary>
    public DateTime CreatedAt { get; protected set; }

    /// <summary>
    /// Gets the date and time when this entity was last modified.
    /// </summary>
    public DateTime? ModifiedAt { get; protected set; }

    /// <summary>
    /// Gets the domain events that have been raised by this entity.
    /// </summary>
    /// <remarks>
    /// These events are used for the Observer pattern - multiple handlers can react to entity state changes.
    /// </remarks>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Adds a domain event to this entity's event collection.
    /// </summary>
    /// <param name="domainEvent">The domain event to add.</param>
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Clears all domain events from this entity.
    /// Should be called after events have been dispatched.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    /// <summary>
    /// Updates the modification timestamp.
    /// Should be called whenever the entity's state changes.
    /// </summary>
    protected void MarkAsModified()
    {
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current entity.
    /// Entities are equal if they have the same ID and are of the same type.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not Entity other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        if (Id == 0 || other.Id == 0)
            return false;

        return Id == other.Id;
    }

    /// <summary>
    /// Returns a hash code for the current entity based on its ID.
    /// </summary>
    public override int GetHashCode()
    {
        return (GetType().ToString() + Id).GetHashCode();
    }

    public static bool operator ==(Entity? left, Entity? right)
    {
        if (left is null && right is null)
            return true;

        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    public static bool operator !=(Entity? left, Entity? right)
    {
        return !(left == right);
    }
}