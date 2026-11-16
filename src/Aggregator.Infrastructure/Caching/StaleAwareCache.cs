using Aggregator.Core.Model;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Aggregator.Infrastructure.Caching;

internal sealed class StaleAwareCache(HybridCache cache, TimeProvider timeProvider, ILogger<StaleAwareCache> logger)
{
    private const string CoolingDown = "cooling-down";

    // HybridCache has no TryGet: a lookup is GetOrCreateAsync with a factory that is never called.
    private static readonly HybridCacheEntryOptions ReadOnly = new() { Flags = HybridCacheEntryFlags.DisableUnderlyingData };

    public Task<SourceResult<T>> GetAsync<T>(
        string key,
        Func<CancellationToken, Task<SourceResult<T>>> fetch,
        TimeSpan freshFor,
        TimeSpan keepLastGoodFor,
        CancellationToken cancellationToken)
        where T : class =>
        GetAsync(key, fetch, new CacheTimes(freshFor, keepLastGoodFor, NoDataFreshFor: null, FailureCooldown: TimeSpan.Zero), cancellationToken);

    public async Task<SourceResult<T>> GetAsync<T>(
        string key,
        Func<CancellationToken, Task<SourceResult<T>>> fetch,
        CacheTimes times,
        CancellationToken cancellationToken)
        where T : class
    {
        var lastGoodKey = $"{key}|last-good";
        var cooldownKey = $"{key}|cooldown";
        var lastGoodFor = times.FreshFor + times.KeepLastGoodFor;
        var calledTheSource = false;

        try
        {
            var result = await cache.GetOrCreateAsync(
                key,
                async token =>
                {
                    // Recently failed: serve the last good value without calling the source again until the cooldown ends.
                    if (times.FailureCooldown > TimeSpan.Zero && await IsSetAsync(cooldownKey, token))
                        throw new SourceUnavailableException();

                    calledTheSource = true;
                    var fetched = await fetch(token);

                    // Throwing is the only way to make HybridCache store nothing, so a failure is never cached.
                    if (fetched.Status == SourceStatus.Unavailable)
                        throw new SourceUnavailableException();

                    // Stamped once here, so a stale copy served later still says when its data was fetched.
                    var stamped = fetched.FetchedOn(timeProvider.GetUtcNow());

                    if (stamped.Value is not null)
                        await cache.SetAsync(lastGoodKey, stamped, Lifetime(lastGoodFor), cancellationToken: token);

                    return stamped;
                },
                Lifetime(times.FreshFor),
                cancellationToken: cancellationToken);

            // HybridCache fixes the lifetime before the fetch, so a no_data answer gets its shorter one afterwards.
            if (calledTheSource && result.Status == SourceStatus.NoData && times.NoDataFreshFor is { } noDataFor && noDataFor < times.FreshFor)
                await cache.SetAsync(key, result, Lifetime(noDataFor), cancellationToken: cancellationToken);

            return result;
        }
        // Any failure except the caller's own cancellation degrades only this source; unexpected ones are logged as errors.
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            if (exception is not SourceUnavailableException)
                logger.UnexpectedFetchFailure(key, exception);

            if (calledTheSource && times.FailureCooldown > TimeSpan.Zero)
                await cache.SetAsync(cooldownKey, CoolingDown, Lifetime(times.FailureCooldown), cancellationToken: cancellationToken);

            // Read-only lookup of the last good value: on a miss nothing is fetched or stored.
            var lastGood = await cache.GetOrCreateAsync<SourceResult<T>?>(
                lastGoodKey,
                _ => ValueTask.FromResult<SourceResult<T>?>(null),
                ReadOnly,
                cancellationToken: cancellationToken);

            return lastGood?.AsStale() ?? SourceResult.Unavailable<T>();
        }
    }

    private static HybridCacheEntryOptions Lifetime(TimeSpan lifetime) => new() { Expiration = lifetime, LocalCacheExpiration = lifetime };

    private async Task<bool> IsSetAsync(string key, CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync<string?>(key, _ => ValueTask.FromResult<string?>(null), ReadOnly, cancellationToken: cancellationToken) is not null;
}
