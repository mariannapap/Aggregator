using Aggregator.Core.Sources;
using Aggregator.Infrastructure.Options;

namespace Aggregator.Infrastructure.Caching;

internal interface ICachePolicy<in TQuery>
    where TQuery : ISourceQuery
{
    TimeSpan FreshFor(TQuery query, SourceClientOptions settings);
}
