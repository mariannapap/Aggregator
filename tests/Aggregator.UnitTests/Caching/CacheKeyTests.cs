using Aggregator.Core.Model;
using Aggregator.Core.Sources;
using Aggregator.Infrastructure.OpenMeteo;

namespace Aggregator.UnitTests.Caching;

[Requirement("R9")]
public sealed class CacheKeyTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
    private static readonly OpenMeteoEndpoint Endpoint = new("test", "v1/test", HourlyCategory.Weather, [new("field_a", "metricA", "unitA")]);

    [Fact]
    public void LocationQuery_RequestsThatDiffer_HaveDifferentKeys_AndEqualKeysMeanEqualRequests()
    {
        LocationQuery[] variants =
        [
            new(new Coordinates(37.98, 23.73), 2, includeCurrent: true, hourly: null),
            new(new Coordinates(37.99, 23.73), 2, includeCurrent: true, hourly: null),
            new(new Coordinates(-37.98, 23.73), 2, includeCurrent: true, hourly: null),
            new(new Coordinates(37.98, 23.74), 2, includeCurrent: true, hourly: null),
            new(new Coordinates(37.981, 23.729), 2, includeCurrent: true, hourly: null),
            new(new Coordinates(37.98, 23.73), 2, includeCurrent: false, new HourlyWindow(Start, 24)),
            new(new Coordinates(37.98, 23.73), 2, includeCurrent: true, new HourlyWindow(Start, 24)),
            new(new Coordinates(37.98, 23.73), 2, includeCurrent: true, new HourlyWindow(Start.AddHours(1), 24)),
            new(new Coordinates(37.98, 23.73), 2, includeCurrent: true, new HourlyWindow(Start, 48))
        ];

        var requestsByKey = variants
            .GroupBy(query => query.CacheKey)
            .ToDictionary(group => group.Key, group => group.Select(query => OpenMeteoLocationClient.BuildUri(Endpoint, query)).Distinct().ToList());

        Assert.All(requestsByKey.Values, requests => Assert.Single(requests));
        Assert.Equal(variants.Select(query => OpenMeteoLocationClient.BuildUri(Endpoint, query)).Distinct().Count(), requestsByKey.Count);
    }

    [Theory]
    [InlineData("Athens", "US")]
    [InlineData("Athina", "GR")]
    [InlineData("Athens Airport", "GR")]
    public void GeocodingQuery_AnotherCityOrCountry_HasAnotherKey(string city, string country) =>
        Assert.NotEqual(new GeocodingQuery("Athens", "GR").CacheKey, new GeocodingQuery(city, country).CacheKey);
}
