using Aggregator.Core.CityReports;
using Aggregator.Core.Model;

namespace Aggregator.UnitTests.CityReports;

public sealed class HourlyFilterTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 15, 15, 0, 0, TimeSpan.Zero);
    private static readonly HourlyWindow Window = new(Start, 24);

    [Fact]
    public void ResolveRange_NoBounds_CoversTheWholeWindow()
    {
        var range = HourlyFilter.ResolveRange(HourlyOptions.Default, Window);

        Assert.Equal(Start, range.AppliedFrom);
        Assert.Equal(Start.AddHours(23), range.AppliedTo);
        Assert.False(range.Clamped);
    }

    [Fact]
    public void ResolveRange_BoundsInsideWindow_AreKept()
    {
        var range = HourlyFilter.ResolveRange(HourlyOptions.Default with { From = Start.AddHours(2), To = Start.AddHours(5) }, Window);

        Assert.Equal((Start.AddHours(2), Start.AddHours(5)), (range.AppliedFrom, range.AppliedTo));
        Assert.False(range.Clamped);
    }

    [Fact]
    public void ResolveRange_BoundsOutsideWindow_AreClampedAndReported()
    {
        var range = HourlyFilter.ResolveRange(HourlyOptions.Default with { From = Start.AddDays(-1), To = Start.AddDays(3) }, Window);

        Assert.Equal((Start, Start.AddHours(23)), (range.AppliedFrom, range.AppliedTo));
        Assert.True(range.Clamped);
        Assert.Equal(Start.AddDays(-1), range.RequestedFrom);
    }

    [Fact]
    public void ResolveRange_OffsetBounds_AreNormalizedToUtc()
    {
        var athensLocal = new DateTimeOffset(2026, 11, 15, 19, 0, 0, TimeSpan.FromHours(2));

        var range = HourlyFilter.ResolveRange(HourlyOptions.Default with { From = athensLocal }, Window);

        Assert.Equal(Start.AddHours(2), range.AppliedFrom);
        Assert.Equal(TimeSpan.Zero, range.AppliedFrom.Offset);
    }

    [Fact]
    public void Apply_KeepsRowsWithinInclusiveBounds()
    {
        var range = new HourlyRange(null, null, Start.AddHours(2), Start.AddHours(4));

        var rows = HourlyFilter.Apply(Rows(HourlyCategory.Weather, "temperature", 24), range, descending: false);

        Assert.Equal([Start.AddHours(2), Start.AddHours(3), Start.AddHours(4)], rows.Select(row => row.Time));
    }

    [Fact]
    public void Apply_Descending_IsLatestFirst()
    {
        var range = HourlyFilter.ResolveRange(HourlyOptions.Default, Window);

        var rows = HourlyFilter.Apply(Rows(HourlyCategory.Weather, "temperature", 24), range, descending: true);

        Assert.Equal(Start.AddHours(23), rows[0].Time);
        Assert.Equal(Start, rows[^1].Time);
    }

    [Fact]
    public void Apply_SameHour_IsOrderedByCategoryThenMetric()
    {
        var range = new HourlyRange(null, null, Start, Start);
        var rows = Rows(HourlyCategory.Marine, "waveHeight", 1)
            .Concat(Rows(HourlyCategory.AirQuality, "usAqi", 1))
            .Concat(Rows(HourlyCategory.Marine, "seaSurfaceTemperature", 1))
            .Concat(Rows(HourlyCategory.Weather, "windSpeed", 1))
            .Concat(Rows(HourlyCategory.Weather, "temperature", 1));

        var ordered = HourlyFilter.Apply(rows, range, descending: false);

        Assert.Equal(["temperature", "windSpeed", "usAqi", "seaSurfaceTemperature", "waveHeight"], ordered.Select(row => row.Metric));
    }

    [Fact]
    public void Apply_NoRows_ReturnsEmpty() =>
        Assert.Empty(HourlyFilter.Apply([], HourlyFilter.ResolveRange(HourlyOptions.Default, Window), descending: false));

    private static IEnumerable<HourlyReading> Rows(HourlyCategory category, string metric, int hours) =>
        Enumerable.Range(0, hours).Select(hour => new HourlyReading(Start.AddHours(hour), category, metric, hour, "unit"));
}
