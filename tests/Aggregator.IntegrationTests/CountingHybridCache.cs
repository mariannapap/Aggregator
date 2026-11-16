using Microsoft.Extensions.Caching.Hybrid;

namespace Aggregator.IntegrationTests;

/// <summary>Counts callers that reach the cache for keys with a prefix, so a test can release a held fetch only after every caller has joined it.</summary>
internal sealed class CountingHybridCache(HybridCache inner, string keyPrefix) : HybridCache
{
    private int entered;

    public int Entered => Volatile.Read(ref entered);

    public override ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var readOnly = options?.Flags?.HasFlag(HybridCacheEntryFlags.DisableUnderlyingData) == true;
        if (!readOnly && key.StartsWith(keyPrefix, StringComparison.Ordinal))
            Interlocked.Increment(ref entered);

        return inner.GetOrCreateAsync(key, state, factory, options, tags, cancellationToken);
    }

    public override ValueTask SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default) =>
        inner.SetAsync(key, value, options, tags, cancellationToken);

    public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) => inner.RemoveAsync(key, cancellationToken);

    public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) => inner.RemoveByTagAsync(tag, cancellationToken);
}
