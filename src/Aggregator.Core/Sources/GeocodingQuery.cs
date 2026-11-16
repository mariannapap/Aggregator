namespace Aggregator.Core.Sources;

public sealed record GeocodingQuery(string City, string Country) : ISourceQuery
{
    public string CacheKey => $"{Country}|{City.ToLowerInvariant()}";
}
