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
        GetAsync(key, key, fetch, new CacheTimes(freshFor, keepLastGoodFor, NoDataFreshFor: null, FailureCooldown: TimeSpan.Zero), cancellationToken);

    public async Task<SourceResult<T>> GetAsync<T>(
        string key,
        string fallbackKey,
        Func<CancellationToken, Task<SourceResult<T>>> fetch,
        CacheTimes times,
        CancellationToken cancellationToken)
        where T : class
    {
        // Markers go first: keys contain user input, so a suffix could be forged through it (a city named "Athens|last-good").
        var lastGoodKey = $"last-good|{fallbackKey}";
        var cooldownKey = $"cooldown|{fallbackKey}";
        var lastGoodFor = times.FreshFor + times.KeepLastGoodFor;
        var calledTheSource = false;

        // Bounds this caller's wait only: a fetch shared with other callers keeps running for them.
        using var deadline = times.CallBudget is { } budget ? new CancellationTokenSource(budget, timeProvider) : null;
        using var waitLimit = deadline is null ? null : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var waitToken = waitLimit?.Token ?? cancellationToken;

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

                    if (stamped.Value is not null && await ReplacesLastGoodAsync(stamped, key, lastGoodKey, token))
                        await cache.SetAsync(lastGoodKey, new LastGood<T>(key, stamped), Lifetime(lastGoodFor), cancellationToken: token);

                    return stamped;
                },
                Lifetime(times.FreshFor),
                cancellationToken: waitToken);

            // HybridCache fixes the lifetime before the fetch, so a no_data answer gets its shorter one afterwards.
            if (calledTheSource && result.Status == SourceStatus.NoData && times.NoDataFreshFor is { } noDataFor && noDataFor < times.FreshFor)
                await cache.SetAsync(key, result, Lifetime(noDataFor), cancellationToken: cancellationToken);

            return result;
        }
        // Out of time is not a source failure (the budget may have gone on queueing behind our own traffic), so no cooldown.
        catch (OperationCanceledException) when (deadline is { IsCancellationRequested: true } && !cancellationToken.IsCancellationRequested)
        {
            logger.BudgetExceeded(key, times.CallBudget.GetValueOrDefault());
            var lastGood = await LastGoodAsync<T>(lastGoodKey, cancellationToken);
            return lastGood?.Result.AsStale() ?? SourceResult.Unavailable<T>();
        }
        // Any failure except the caller's own cancellation degrades only this source; unexpected ones are logged as errors.
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            if (exception is not SourceUnavailableException)
                logger.UnexpectedFetchFailure(key, exception);

            if (calledTheSource && times.FailureCooldown > TimeSpan.Zero)
                await cache.SetAsync(cooldownKey, CoolingDown, Lifetime(times.FailureCooldown), cancellationToken: cancellationToken);

            var lastGood = await LastGoodAsync<T>(lastGoodKey, cancellationToken);
            return lastGood?.Result.AsStale() ?? SourceResult.Unavailable<T>();
        }
    }

    private static HybridCacheEntryOptions Lifetime(TimeSpan lifetime) => new() { Expiration = lifetime, LocalCacheExpiration = lifetime };

    // A partial answer never replaces a complete one to the same query; an answer to another query (a later hourly window) always does.
    private async Task<bool> ReplacesLastGoodAsync<T>(SourceResult<T> fetched, string key, string lastGoodKey, CancellationToken cancellationToken)
        where T : class
    {
        if (fetched.Status != SourceStatus.Partial)
            return true;

        var current = await LastGoodAsync<T>(lastGoodKey, cancellationToken);
        return current is not { Result.Status: SourceStatus.Ok } || current.FreshKey != key;
    }

    // Read-only lookup: on a miss nothing is fetched or stored.
    private async Task<LastGood<T>?> LastGoodAsync<T>(string lastGoodKey, CancellationToken cancellationToken)
        where T : class =>
        await cache.GetOrCreateAsync<LastGood<T>?>(lastGoodKey, _ => ValueTask.FromResult<LastGood<T>?>(null), ReadOnly, cancellationToken: cancellationToken);

    private async Task<bool> IsSetAsync(string key, CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync<string?>(key, _ => ValueTask.FromResult<string?>(null), ReadOnly, cancellationToken: cancellationToken) is not null;
}
