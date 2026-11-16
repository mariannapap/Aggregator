using Aggregator.Core.Model;

namespace Aggregator.Infrastructure.Caching;

// FreshKey records which exact query produced the value, so a partial answer to the same query cannot replace a complete one.
internal sealed record LastGood<T>(string FreshKey, SourceResult<T> Result)
    where T : class;
