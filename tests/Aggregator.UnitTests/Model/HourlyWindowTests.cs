using Aggregator.Core.Model;
using Aggregator.Core.Sources;

namespace Aggregator.UnitTests.Model;

public sealed class HourlyWindowTests
{
    [Fact]
    [Requirement("R5")]
    public void StartingAtCurrentHour_TruncatesToTheHourInUtc()
    {
        var window = HourlyWindow.StartingAtCurrentHour(new DateTimeOffset(2026, 11, 15, 17, 45, 12, TimeSpan.FromHours(2)), 24);

        Assert.Equal(new DateTimeOffset(2026, 11, 15, 15, 0, 0, TimeSpan.Zero), window.Start);
        Assert.Equal(new DateTimeOffset(2026, 11, 16, 14, 0, 0, TimeSpan.Zero), window.LastHour);
    }

    [Fact]
    [Requirement("R5")]
    public void Contains_IncludesStartAndExcludesEnd()
    {
        var window = new HourlyWindow(new DateTimeOffset(2026, 11, 15, 15, 0, 0, TimeSpan.Zero), 24);

        Assert.True(window.Contains(window.Start));
        Assert.True(window.Contains(window.LastHour));
        Assert.False(window.Contains(window.End));
    }

    [Fact]
    [Requirement("R9")]
    public void LocationQuery_RoundsCoordinatesOnceForKeyAndRequest()
    {
        var query = new LocationQuery(new Coordinates(37.98376, 23.72784), 2, includeCurrent: true, hourly: null);

        Assert.Equal(new Coordinates(37.98, 23.73), query.Coordinates);
        Assert.Equal("37.98|23.73|current=True|hourly=|", query.CacheKey);
    }

    [Fact]
    [Requirement("R9")]
    public void GeocodingQuery_KeyIgnoresTheCaseOfTheCity() =>
        Assert.Equal(new GeocodingQuery("athens", "GR").CacheKey, new GeocodingQuery("ATHENS", "GR").CacheKey);
}
