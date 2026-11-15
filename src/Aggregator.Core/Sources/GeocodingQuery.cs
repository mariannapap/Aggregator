namespace Aggregator.Core.Sources;

public sealed record GeocodingQuery : ISourceQuery
{
    public GeocodingQuery(string city, string country)
    {
        City = city.Trim();
        Country = country.Trim().ToUpperInvariant();
    }

    public string City { get; }

    public string Country { get; }

    public string CacheKey => $"{Country}|{City.ToLowerInvariant()}";
}
