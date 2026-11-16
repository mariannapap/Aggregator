namespace Aggregator.Core.Sources;

public interface ISourceQuery
{
    string CacheKey { get; }

    // The key of the last good value kept for fallback: queries whose answers can stand in for each other share it.
    string FallbackKey => CacheKey;
}
