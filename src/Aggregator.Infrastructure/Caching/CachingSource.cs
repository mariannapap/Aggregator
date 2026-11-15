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
        return cache.GetAsync(
            $"{inner.Name}|{query.CacheKey}",
            token => inner.FetchAsync(query, token),
            policy.FreshFor(query, settings),
            settings.LastGoodRetention,
            cancellationToken);
    }
}
