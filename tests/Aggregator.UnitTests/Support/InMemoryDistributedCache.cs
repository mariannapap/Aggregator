using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;

namespace Aggregator.UnitTests.Support;

/// <summary>A stand-in for Redis: HybridCache ignores MemoryDistributedCache as L2, so tests that need real serialization use this.</summary>
internal sealed class InMemoryDistributedCache(TimeProvider timeProvider) : IDistributedCache
{
    private readonly ConcurrentDictionary<string, (byte[] Value, DateTimeOffset? ExpiresAt)> entries = new(StringComparer.Ordinal);

    public byte[]? Get(string key) =>
        entries.TryGetValue(key, out var entry) && (entry.ExpiresAt is null || entry.ExpiresAt > timeProvider.GetUtcNow()) ? entry.Value : null;

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        var now = timeProvider.GetUtcNow();
        DateTimeOffset? expiresAt = options.AbsoluteExpiration
            ?? (options.AbsoluteExpirationRelativeToNow is { } relative ? now + relative : null);
        entries[key] = (value, expiresAt);
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }

    public void Refresh(string key)
    {
    }

    public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

    public void Remove(string key) => entries.TryRemove(key, out _);

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }

    public int Count => entries.Count;
}
