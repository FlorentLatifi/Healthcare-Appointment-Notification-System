using Healthcare.Application.Ports.Caching;
using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Healthcare.Adapters.Events.Handlers;

/// <summary>
/// Clears day-level availability cache when appointments change.
/// </summary>
public sealed class InvalidateAvailabilityCacheOnCreatedHandler
    : IDomainEventHandler<AppointmentCreatedEvent>
{
    private readonly IAvailabilityCacheService _availabilityCache;
    private readonly ILogger<InvalidateAvailabilityCacheOnCreatedHandler> _logger;

    public InvalidateAvailabilityCacheOnCreatedHandler(
        IAvailabilityCacheService availabilityCache,
        ILogger<InvalidateAvailabilityCacheOnCreatedHandler> logger)
    {
        _availabilityCache = availabilityCache;
        _logger = logger;
    }

    public async Task HandleAsync(AppointmentCreatedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var date = DateOnly.FromDateTime(domainEvent.ScheduledTime.ToUniversalTime().Date);
        _logger.LogInformation(
            "Invalidating availability for doctor {DoctorId} on {Date} (appointment created)",
            domainEvent.DoctorId, date);
        await _availabilityCache.InvalidateDayAsync(domainEvent.DoctorId, date, cancellationToken);
    }
}

public sealed class InvalidateAvailabilityCacheOnCancelledHandler
    : IDomainEventHandler<AppointmentCancelledEvent>
{
    private readonly IAvailabilityCacheService _availabilityCache;
    private readonly ILogger<InvalidateAvailabilityCacheOnCancelledHandler> _logger;

    public InvalidateAvailabilityCacheOnCancelledHandler(
        IAvailabilityCacheService availabilityCache,
        ILogger<InvalidateAvailabilityCacheOnCancelledHandler> logger)
    {
        _availabilityCache = availabilityCache;
        _logger = logger;
    }

    public async Task HandleAsync(AppointmentCancelledEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var date = DateOnly.FromDateTime(domainEvent.ScheduledTime.ToUniversalTime().Date);
        _logger.LogInformation(
            "Invalidating availability for doctor {DoctorId} on {Date} (appointment cancelled)",
            domainEvent.DoctorId, date);
        await _availabilityCache.InvalidateDayAsync(domainEvent.DoctorId, date, cancellationToken);
    }
}

public sealed class InvalidateAvailabilityCacheOnConfirmedHandler
    : IDomainEventHandler<AppointmentConfirmedEvent>
{
    private readonly IAvailabilityCacheService _availabilityCache;

    public InvalidateAvailabilityCacheOnConfirmedHandler(IAvailabilityCacheService availabilityCache)
    {
        _availabilityCache = availabilityCache;
    }

    public async Task HandleAsync(AppointmentConfirmedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var date = DateOnly.FromDateTime(domainEvent.ScheduledTime.ToUniversalTime().Date);
        await _availabilityCache.InvalidateDayAsync(domainEvent.DoctorId, date, cancellationToken);
    }
}

public sealed class InvalidateAvailabilityCacheOnNoShowHandler
    : IDomainEventHandler<AppointmentNoShowEvent>
{
    private readonly IAvailabilityCacheService _availabilityCache;

    public InvalidateAvailabilityCacheOnNoShowHandler(IAvailabilityCacheService availabilityCache)
    {
        _availabilityCache = availabilityCache;
    }

    public async Task HandleAsync(AppointmentNoShowEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var date = DateOnly.FromDateTime(domainEvent.ScheduledTime.ToUniversalTime().Date);
        await _availabilityCache.InvalidateDayAsync(domainEvent.DoctorId, date, cancellationToken);
    }
}

public sealed class InvalidateAvailabilityCacheOnCompletedHandler
    : IDomainEventHandler<AppointmentCompletedEvent>
{
    private readonly IAvailabilityCacheService _availabilityCache;

    public InvalidateAvailabilityCacheOnCompletedHandler(IAvailabilityCacheService availabilityCache)
    {
        _availabilityCache = availabilityCache;
    }

    public async Task HandleAsync(AppointmentCompletedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var date = DateOnly.FromDateTime(domainEvent.ScheduledTime.ToUniversalTime().Date);
        await _availabilityCache.InvalidateDayAsync(domainEvent.DoctorId, date, cancellationToken);
    }
}
