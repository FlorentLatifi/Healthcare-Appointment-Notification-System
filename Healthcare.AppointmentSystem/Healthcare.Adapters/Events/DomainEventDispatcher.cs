using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Healthcare.Adapters.Events;

/// <summary>
/// Dispatches domain events to their registered handlers.
/// </summary>
/// <remarks>
/// Design Pattern: Observer Pattern + Mediator Pattern + Adapter Pattern
/// 
/// How it works:
/// 1. Domain entity raises event (e.g., AppointmentConfirmedEvent)
/// 2. Dispatcher finds ALL handlers for that event type
/// 3. Invokes each handler asynchronously
/// 4. Handles failures gracefully (one fails, others continue)
/// 
/// Registration:
/// Handlers are registered in DI container as:
/// services.AddScoped<IDomainEventHandler<AppointmentConfirmedEvent>, SendNotificationHandler>();
/// 
/// Thread Safety:
/// - Handlers are resolved per-scope (safe)
/// - Multiple events can be dispatched concurrently
/// 
/// Error Handling:
/// - Logs all errors
/// - Doesn't throw (resilient)
/// - Critical for production reliability
/// </remarks>
public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(
        IServiceProvider serviceProvider,
        ILogger<DomainEventDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Dispatches a single domain event to all registered handlers.
    /// </summary>
    public async Task DispatchAsync<TEvent>(
        TEvent domainEvent,
        CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
    {
        if (domainEvent == null)
        {
            _logger.LogWarning("Attempted to dispatch null domain event");
            return;
        }

        var eventType = domainEvent.GetType();
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);

        _logger.LogInformation(
            "Dispatching domain event {EventType} with ID {EventId}",
            eventType.Name,
            domainEvent.EventId);

        // Get all handlers for this event type from DI container
        using var scope = _serviceProvider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices(handlerType);

        var handlersList = handlers.ToList();

        if (!handlersList.Any())
        {
            _logger.LogWarning(
                "No handlers registered for event type {EventType}",
                eventType.Name);
            return;
        }

        _logger.LogDebug(
            "Found {Count} handler(s) for event {EventType}",
            handlersList.Count,
            eventType.Name);

        // Invoke each handler
        var tasks = handlersList.Select(async handler =>
        {
            try
            {
                // Use reflection to call HandleAsync method
                var handleMethod = handlerType.GetMethod(nameof(IDomainEventHandler<TEvent>.HandleAsync));
                if (handleMethod != null)
                {
                    var task = (Task?)handleMethod.Invoke(handler, new object[] { domainEvent, cancellationToken });
                    if (task != null)
                    {
                        await task;
                    }
                }

                _logger.LogDebug(
                    "Handler {HandlerType} completed for event {EventType}",
                    handler.GetType().Name,
                    eventType.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Handler {HandlerType} failed for event {EventType} with ID {EventId}",
                    handler.GetType().Name,
                    eventType.Name,
                    domainEvent.EventId);

                // Don't throw - let other handlers continue (resilience)
            }
        });

        await Task.WhenAll(tasks);

        _logger.LogInformation(
            "Domain event {EventType} dispatched to {Count} handler(s)",
            eventType.Name,
            handlersList.Count);
    }

    /// <summary>
    /// Dispatches multiple domain events sequentially.
    /// </summary>
    public async Task DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        var eventsList = domainEvents.ToList();

        if (!eventsList.Any())
        {
            _logger.LogDebug("No domain events to dispatch");
            return;
        }

        _logger.LogInformation(
            "Dispatching {Count} domain event(s)",
            eventsList.Count);

        // Dispatch events sequentially to maintain order
        foreach (var domainEvent in eventsList)
        {
            // Use dynamic dispatch to preserve generic type
            await DispatchDynamicAsync((dynamic)domainEvent, cancellationToken);
        }

        _logger.LogInformation(
            "All {Count} domain event(s) dispatched successfully",
            eventsList.Count);
    }

    /// <summary>
    /// Helper method for dynamic dispatch (preserves generic type).
    /// </summary>
    private Task DispatchDynamicAsync<TEvent>(
        TEvent domainEvent,
        CancellationToken cancellationToken)
        where TEvent : IDomainEvent
    {
        return DispatchAsync(domainEvent, cancellationToken);
    }
}