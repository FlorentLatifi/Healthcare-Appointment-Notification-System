using Healthcare.Domain.Common;

namespace Healthcare.Application.Ports.Events;

/// <summary>
/// Service for dispatching domain events to their handlers.
/// </summary>
/// <remarks>
/// Design Pattern: Mediator Pattern + Observer Pattern
/// 
/// The dispatcher acts as a mediator between domain entities (which raise events)
/// and event handlers (which react to events). This decouples the domain from
/// the application layer.
/// 
/// Hexagonal Architecture: This is a PORT. The ADAPTER will be implemented
/// in the Infrastructure layer and will use dependency injection to find
/// all registered handlers for each event type.
/// </remarks>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Dispatches a domain event to all registered handlers.
    /// </summary>
    /// <typeparam name="TEvent">The type of domain event.</typeparam>
    /// <param name="domainEvent">The event to dispatch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DispatchAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent;

    /// <summary>
    /// Dispatches multiple domain events to their handlers.
    /// </summary>
    /// <param name="domainEvents">The events to dispatch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}