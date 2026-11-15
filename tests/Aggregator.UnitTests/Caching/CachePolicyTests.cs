using Aggregator.Core.Model;
using Aggregator.Core.Sources;
using Aggregator.Infrastructure.Caching;
using Aggregator.Infrastructure.Options;

namespace Aggregator.UnitTests.Caching;

[Requirement("R9")]
public sealed class CachePolicyTests
{
    private static readonly SourceClientOptions Settings = new()
    {
        CacheTtl = TimeSpan.FromMinutes(15),
        HourlyCacheTtl = TimeSpan.FromHours(1)
    };

    [Fact]
    public void Geocoding_UsesTheStandardTtl() =>
        Assert.Equal(TimeSpan.FromMinutes(15), new GeocodingCachePolicy().FreshFor(new GeocodingQuery("Athens", "GR"), Settings));

    [Theory]
    [InlineData(true, false, 15)]
    [InlineData(false, true, 60)]
    [InlineData(true, true, 15)]
    public void Location_UsesTheTtlOfWhatIsRequested_AndTheShorterOneWhenCombined(bool includeCurrent, bool includeHourly, int expectedMinutes)
    {
        var window = includeHourly ? new HourlyWindow(DateTimeOffset.UnixEpoch, 24) : null;
        var query = new LocationQuery(new Coordinates(1, 1), 2, includeCurrent, window);

        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), new LocationCachePolicy().FreshFor(query, Settings));
    }

    [Fact]
    public void Location_CombinedQuery_UsesHourlyTtlWhenItIsTheShorterOne()
    {
        var settings = new SourceClientOptions { CacheTtl = TimeSpan.FromHours(2), HourlyCacheTtl = TimeSpan.FromMinutes(30) };
        var query = new LocationQuery(new Coordinates(1, 1), 2, includeCurrent: true, new HourlyWindow(DateTimeOffset.UnixEpoch, 24));

        Assert.Equal(TimeSpan.FromMinutes(30), new LocationCachePolicy().FreshFor(query, settings));
    }
}
