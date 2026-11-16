using System.Diagnostics;
using Aggregator.Core.Model;
using Aggregator.Core.Sources;
using Aggregator.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Aggregator.Infrastructure.Caching;

internal sealed class CachingSource<TQuery, TResult>(
    IExternalSource<TQuery, TResult> inner,
    StaleAwareCache cache,
    ICachePolicy<TQuery> policy,
    IOptionsMonitor<SourceClientOptions> options) : IExternalSource<TQuery, TResult>
    where TQuery : ISourceQuery
    where TResult : class
{
    public string Name => inner.Name;

    public Task<SourceResult<TResult>> FetchAsync(TQuery query, CancellationToken cancellationToken)
    {
        var settings = options.Get(inner.Name);
        var key = $"{inner.Name}|{query.CacheKey}";
        var caller = Activity.Current?.Context ?? default;
        return cache.GetAsync(
            key,
            $"{inner.Name}|{query.FallbackKey}",
            async token =>
            {
                // HybridCache runs the shared fetch without the caller's context; this span reconnects it to the caller's trace.
                using var activity = CacheTelemetry.Source.StartActivity($"fetch {inner.Name}", ActivityKind.Internal, caller);
                activity?.SetTag("cache.key", key);
                return await inner.FetchAsync(query, token);
            },
            new CacheTimes(policy.FreshFor(query, settings), settings.LastGoodRetention, settings.NoDataCacheTtl, settings.FailureCooldown, settings.CallBudget),
            cancellationToken);
    }
}
