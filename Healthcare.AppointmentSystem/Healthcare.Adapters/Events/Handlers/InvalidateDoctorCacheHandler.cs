using Healthcare.Application.Ports.Caching;
using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Healthcare.Adapters.Events.Handlers;

public sealed class InvalidateDoctorCacheHandler : IDomainEventHandler<DoctorCacheInvalidationNeededEvent>
{
    private readonly IDoctorCacheService _doctorCache;
    private readonly IAvailabilityCacheService _availabilityCache;
    private readonly ILogger<InvalidateDoctorCacheHandler> _logger;

    public InvalidateDoctorCacheHandler(
        IDoctorCacheService doctorCache,
        IAvailabilityCacheService availabilityCache,
        ILogger<InvalidateDoctorCacheHandler> logger)
    {
        _doctorCache = doctorCache;
        _availabilityCache = availabilityCache;
        _logger = logger;
    }

    public async Task HandleAsync(
        DoctorCacheInvalidationNeededEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Invalidating doctor cache (DoctorId={DoctorId}, EventId={EventId})",
            domainEvent.DoctorId, domainEvent.EventId);

        await _doctorCache.InvalidateDoctorAsync(domainEvent.DoctorId, cancellationToken);

        if (domainEvent.DoctorId is int id)
            await _availabilityCache.InvalidateDoctorAsync(id, cancellationToken);
    }
}
