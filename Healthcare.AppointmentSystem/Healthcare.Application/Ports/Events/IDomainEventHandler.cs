using Healthcare.Domain.Common;

namespace Healthcare.Application.Ports.Events;

/// <summary>
/// Defines a handler for a domain event.
/// </summary>
/// <typeparam name="TEvent">The type of domain event to handle.</typeparam>
/// <remarks>
/// Design Pattern: Observer Pattern
/// 
/// Domain event handlers are OBSERVERS that react to domain events.
/// Multiple handlers can observe the same event.
/// 
/// Example: When AppointmentConfirmedEvent is raised:
/// - SendConfirmationNotificationHandler sends email/SMS
/// - LogAppointmentConfirmedHandler logs to audit trail
/// - UpdateAnalyticsHandler updates statistics
/// 
/// All handlers run independently - if one fails, others continue.
/// </remarks>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    /// <summary>
    /// Handles the domain event asynchronously.
    /// </summary>
    /// <param name="domainEvent">The domain event to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}