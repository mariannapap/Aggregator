using Aggregator.Core.Sources;
using Aggregator.Infrastructure.Options;

namespace Aggregator.Infrastructure.Caching;

internal sealed class GeocodingCachePolicy : ICachePolicy<GeocodingQuery>
{
    public TimeSpan FreshFor(GeocodingQuery query, SourceClientOptions settings) => settings.CacheTtl;
}
