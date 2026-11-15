using Aggregator.Core.Sources;
using Aggregator.Infrastructure.Options;

namespace Aggregator.Infrastructure.Caching;

internal sealed class LocationCachePolicy : ICachePolicy<LocationQuery>
{
    public TimeSpan FreshFor(LocationQuery query, SourceClientOptions settings) => (query.IncludesCurrent, query.IncludesHourly) switch
    {
        (true, true) => settings.CacheTtl < settings.HourlyCacheTtl ? settings.CacheTtl : settings.HourlyCacheTtl,
        (false, true) => settings.HourlyCacheTtl,
        _ => settings.CacheTtl
    };
}
