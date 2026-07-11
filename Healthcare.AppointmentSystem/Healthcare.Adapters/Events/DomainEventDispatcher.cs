using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Healthcare.Adapters.Events;

/// <summary>
/// Dispatches domain events to their registered handlers.
/// </summary>
/// <remarks>
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
    /// Handler failures are logged and swallowed (in-process resilience).
    /// For outbox relay use <see cref="DispatchStrictAsync{TEvent}"/>.
    /// </summary>
    public Task DispatchAsync<TEvent>(
        TEvent domainEvent,
        CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
        => DispatchCoreAsync(domainEvent, throwOnHandlerFailure: false, cancellationToken);

    /// <summary>
    /// Strict dispatch for outbox: if any handler fails, throws so the message is retried.
    /// Other handlers still run; exceptions are aggregated.
    /// </summary>
    public Task DispatchStrictAsync<TEvent>(
        TEvent domainEvent,
        CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
        => DispatchCoreAsync(domainEvent, throwOnHandlerFailure: true, cancellationToken);

    /// <summary>
    /// Dispatches multiple domain events sequentially (best-effort per event).
    /// </summary>
    public async Task DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        var eventsList = domainEvents.ToList();

        if (eventsList.Count == 0)
        {
            _logger.LogDebug("No domain events to dispatch");
            return;
        }

        _logger.LogInformation("Dispatching {Count} domain event(s)", eventsList.Count);

        foreach (var domainEvent in eventsList)
            await DispatchDynamicAsync((dynamic)domainEvent, false, cancellationToken);

        _logger.LogInformation("All {Count} domain event(s) dispatched", eventsList.Count);
    }

    /// <summary>
    /// Strict sequential dispatch for outbox batches (fails the current message on handler error).
    /// </summary>
    public async Task DispatchStrictAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
            await DispatchDynamicAsync((dynamic)domainEvent, true, cancellationToken);
    }

    private async Task DispatchCoreAsync<TEvent>(
        TEvent domainEvent,
        bool throwOnHandlerFailure,
        CancellationToken cancellationToken)
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
            "Dispatching domain event {EventType} with ID {EventId} (strict={Strict})",
            eventType.Name,
            domainEvent.EventId,
            throwOnHandlerFailure);

        using var scope = _serviceProvider.CreateScope();
        var handlersList = scope.ServiceProvider.GetServices(handlerType).ToList();

        if (handlersList.Count == 0)
        {
            _logger.LogWarning("No handlers registered for event type {EventType}", eventType.Name);
            return;
        }

        var failures = new List<Exception>();

        foreach (var handler in handlersList)
        {
            try
            {
                var handleMethod = handlerType.GetMethod(nameof(IDomainEventHandler<TEvent>.HandleAsync));
                if (handleMethod is null)
                    continue;

                var task = (Task?)handleMethod.Invoke(handler, new object[] { domainEvent, cancellationToken });
                if (task is not null)
                    await task;

                _logger.LogDebug(
                    "Handler {HandlerType} completed for event {EventType}",
                    handler!.GetType().Name,
                    eventType.Name);
            }
            catch (Exception ex)
            {
                var inner = ex is System.Reflection.TargetInvocationException { InnerException: { } tie }
                    ? tie
                    : ex;

                _logger.LogError(
                    inner,
                    "Handler {HandlerType} failed for event {EventType} with ID {EventId}",
                    handler!.GetType().Name,
                    eventType.Name,
                    domainEvent.EventId);

                if (throwOnHandlerFailure)
                    failures.Add(inner);
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                $"One or more handlers failed for {eventType.Name} ({domainEvent.EventId}).",
                failures);
        }

        _logger.LogInformation(
            "Domain event {EventType} dispatched to {Count} handler(s)",
            eventType.Name,
            handlersList.Count);
    }

    private Task DispatchDynamicAsync<TEvent>(
        TEvent domainEvent,
        bool throwOnHandlerFailure,
        CancellationToken cancellationToken)
        where TEvent : IDomainEvent
        => DispatchCoreAsync(domainEvent, throwOnHandlerFailure, cancellationToken);
}