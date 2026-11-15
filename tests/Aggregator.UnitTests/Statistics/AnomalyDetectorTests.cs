using Aggregator.Core.Statistics;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Aggregator.UnitTests.Statistics;

public sealed class AnomalyDetectorTests
{
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 15, 15, 0, 0, TimeSpan.Zero));
    private readonly StatisticsStore store;
    private readonly AnomalyDetector detector;

    public AnomalyDetectorTests()
    {
        store = new StatisticsStore(Options.Create(new StatisticsOptions()), time);
        detector = new AnomalyDetector(store, Options.Create(new AnomalyOptions { WindowMinutes = 5, Ratio = 1.5, MinSamples = 5 }));
    }

    [Fact]
    public void Detect_RecentAverageAboveThreshold_ReportsAnomaly()
    {
        RecordMany(10, 100);
        time.Advance(TimeSpan.FromMinutes(10));
        RecordMany(5, 200);

        var anomaly = Assert.Single(detector.Detect());

        Assert.Equal("weather", anomaly.Api);
        Assert.Equal(200, anomaly.RecentAverageMs);
        Assert.Equal(100, anomaly.BaselineAverageMs);
        Assert.Equal(5, anomaly.RecentCount);
    }

    [Fact]
    public void Detect_RecentAverageWithinThreshold_ReportsNothing()
    {
        RecordMany(10, 100);
        time.Advance(TimeSpan.FromMinutes(10));
        RecordMany(5, 140);

        Assert.Empty(detector.Detect());
    }

    [Fact]
    public void Detect_TooFewRecentSamples_ReportsNothing()
    {
        RecordMany(10, 100);
        time.Advance(TimeSpan.FromMinutes(10));
        RecordMany(4, 1_000);

        Assert.Empty(detector.Detect());
    }

    [Fact]
    public void Detect_NoBaselineYet_ReportsNothing()
    {
        RecordMany(10, 1_000);

        Assert.Empty(detector.Detect());
    }

    private void RecordMany(int count, double milliseconds)
    {
        for (var i = 0; i < count; i++)
            store.Record("weather", TimeSpan.FromMilliseconds(milliseconds));
    }
}
