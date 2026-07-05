using Healthcare.Application.Ports.Caching;
using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Healthcare.Adapters.Events.Handlers;

public sealed class InvalidateDoctorCacheHandler : IDomainEventHandler<DoctorCacheInvalidationNeededEvent>
{
    private readonly IDoctorCacheService _cacheService;
    private readonly ILogger<InvalidateDoctorCacheHandler> _logger;

    public InvalidateDoctorCacheHandler(
        IDoctorCacheService cacheService,
        ILogger<InvalidateDoctorCacheHandler> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task HandleAsync(
        DoctorCacheInvalidationNeededEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Invalidating doctor cache due to event {EventId}",
            domainEvent.EventId);

        await _cacheService.InvalidateAllAsync(cancellationToken);
    }
}
