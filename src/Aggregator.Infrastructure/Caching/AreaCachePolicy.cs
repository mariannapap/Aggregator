using Aggregator.Core.Sources;
using Aggregator.Infrastructure.Options;

namespace Aggregator.Infrastructure.Caching;

internal sealed class AreaCachePolicy : ICachePolicy<AreaQuery>
{
    public TimeSpan FreshFor(AreaQuery query, SourceClientOptions settings) => settings.CacheTtl;
}
