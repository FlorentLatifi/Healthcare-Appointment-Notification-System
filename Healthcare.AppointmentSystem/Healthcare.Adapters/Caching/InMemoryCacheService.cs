using System.Collections.Concurrent;
using System.Text.Json;
using Healthcare.Application.Ports.Caching;
using Microsoft.Extensions.Logging;

namespace Healthcare.Adapters.Caching;

/// <summary>
/// In-process cache-aside with per-key single-flight (dev/test).
/// </summary>
public sealed class InMemoryCacheService : ICacheService, IDisposable
{
    private readonly ConcurrentDictionary<string, Entry> _store = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();
    private readonly ConcurrentDictionary<string, long> _generations = new();
    private readonly CacheSettings _settings;
    private readonly ILogger<InMemoryCacheService> _logger;
    private readonly JsonSerializerOptions _json;

    private sealed class Entry
    {
        public required string Json { get; init; }
        public required DateTime ExpiresAtUtc { get; init; }
        public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    }

    public InMemoryCacheService(CacheSettings settings, ILogger<InMemoryCacheService> logger)
    {
        _settings = settings;
        _logger = logger;
        _json = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public Task<CacheLookup<T>> TryGetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
            return Task.FromResult(CacheLookup<T>.Miss());

        if (_store.TryGetValue(key, out var entry) && !entry.IsExpired)
        {
            var value = JsonSerializer.Deserialize<T>(entry.Json, _json);
            return Task.FromResult(CacheLookup<T>.Hit(value));
        }

        if (entry is not null)
            _store.TryRemove(key, out _);

        return Task.FromResult(CacheLookup<T>.Miss());
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
            return Task.CompletedTask;

        var json = JsonSerializer.Serialize(value, _json);
        _store[key] = new Entry
        {
            Json = json,
            ExpiresAtUtc = DateTime.UtcNow.Add(ttl ?? _settings.DefaultTtl)
        };
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string keyPrefix, CancellationToken cancellationToken = default)
    {
        foreach (var key in _store.Keys)
        {
            if (key.StartsWith(keyPrefix, StringComparison.Ordinal))
                _store.TryRemove(key, out _);
        }
        return Task.CompletedTask;
    }

    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
            return await factory(cancellationToken);

        var existing = await TryGetAsync<T>(key, cancellationToken);
        if (existing.Found)
            return existing.Value;

        var gate = _gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        var acquired = await gate.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        if (!acquired)
        {
            for (var i = 1; i <= _settings.StampedeWaitAttempts; i++)
            {
                await Task.Delay(_settings.StampedeWaitBaseDelayMs * i, cancellationToken);
                existing = await TryGetAsync<T>(key, cancellationToken);
                if (existing.Found)
                    return existing.Value;
            }

            return await factory(cancellationToken);
        }

        try
        {
            existing = await TryGetAsync<T>(key, cancellationToken);
            if (existing.Found)
                return existing.Value;

            var value = await factory(cancellationToken);
            if (value is not null)
                await SetAsync(key, value, ttl, cancellationToken);
            return value;
        }
        finally
        {
            gate.Release();
        }
    }

    public Task<long> IncrementGenerationAsync(string generationKey, CancellationToken cancellationToken = default)
    {
        var next = _generations.AddOrUpdate(generationKey, 1, (_, v) => v + 1);
        return Task.FromResult(next);
    }

    public Task<long> GetGenerationAsync(string generationKey, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_generations.TryGetValue(generationKey, out var g) ? g : 0L);
    }

    public void Dispose()
    {
        foreach (var g in _gates.Values)
            g.Dispose();
        _gates.Clear();
        _store.Clear();
    }
}
