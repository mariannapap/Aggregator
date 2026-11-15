using Aggregator.Core.Model;
using Microsoft.Extensions.Caching.Hybrid;

namespace Aggregator.Infrastructure.Caching;

internal sealed class StaleAwareCache(HybridCache cache)
{
    private static readonly HybridCacheEntryOptions ReadOnly = new() { Flags = HybridCacheEntryFlags.DisableUnderlyingData };

    public async Task<SourceResult<T>> GetAsync<T>(
        string key,
        Func<CancellationToken, Task<SourceResult<T>>> fetch,
        TimeSpan freshFor,
        TimeSpan keepLastGoodFor,
        CancellationToken cancellationToken)
        where T : class
    {
        var lastGoodKey = $"{key}|last-good";
        var freshOptions = new HybridCacheEntryOptions { Expiration = freshFor, LocalCacheExpiration = freshFor };
        var lastGoodOptions = new HybridCacheEntryOptions { Expiration = freshFor + keepLastGoodFor, LocalCacheExpiration = freshFor + keepLastGoodFor };

        try
        {
            return await cache.GetOrCreateAsync(
                key,
                async token =>
                {
                    var result = await fetch(token);
                    if (result.Status == SourceStatus.Unavailable)
                        throw new SourceUnavailableException();

                    if (result.Value is not null)
                        await cache.SetAsync(lastGoodKey, result, lastGoodOptions, cancellationToken: token);

                    return result;
                },
                freshOptions,
                cancellationToken: cancellationToken);
        }
        catch (SourceUnavailableException) when (!cancellationToken.IsCancellationRequested)
        {
            var lastGood = await cache.GetOrCreateAsync<SourceResult<T>?>(
                lastGoodKey,
                _ => ValueTask.FromResult<SourceResult<T>?>(null),
                ReadOnly,
                cancellationToken: cancellationToken);

            return lastGood?.AsStale() ?? SourceResult.Unavailable<T>();
        }
    }
}
