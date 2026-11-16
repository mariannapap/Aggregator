using Aggregator.Core.Statistics;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Aggregator.UnitTests.Statistics;

// Requests recorded right after an Advance land in the current minute; the next Advance(OneMinute) completes it.
[Requirement("O2")]
public sealed class AnomalyDetectorTests
{
    private static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 15, 15, 0, 0, TimeSpan.Zero));
    private readonly StatisticsStore store;
    private readonly AnomalyDetector detector;

    public AnomalyDetectorTests()
    {
        store = new StatisticsStore(Options.Create(new StatisticsOptions()), time, [new MonitoredApi("weather")]);
        detector = Detector(minimumIncreaseMs: 50);
    }

    [Fact]
    public void Assess_RecentAverageAboveThreshold_IsAnomalous()
    {
        RecordMany(10, 100);
        time.Advance(TimeSpan.FromMinutes(10));
        RecordMany(5, 200);
        time.Advance(OneMinute);

        var weather = Assert.Single(detector.Assess());

        Assert.Equal(new ApiPerformance("weather", AnomalyState.Anomalous, 5, 200, 10, 100), weather);
    }

    [Fact]
    public void Assess_RecentAverageWithinThreshold_IsNormal() =>
        Assert.Equal(AnomalyState.Normal, StateAfter(baselineMs: 100, recentMs: 140));

    [Fact]
    public void Assess_RelativelySlowerButOnlyByMilliseconds_IsNormal() =>
        Assert.Equal(AnomalyState.Normal, StateAfter(baselineMs: 0.56, recentMs: 1.46));

    [Theory]
    [InlineData(69, AnomalyState.Normal)]
    [InlineData(70, AnomalyState.Anomalous)]
    public void Assess_IncreaseAtLeastTheMinimum_IsAnomalous(double recentMs, AnomalyState expected) =>
        Assert.Equal(expected, StateAfter(baselineMs: 20, recentMs: recentMs));

    [Fact]
    public void Assess_WithoutMinimumIncrease_UsesTheRatioAlone() =>
        Assert.Equal(AnomalyState.Anomalous, StateAfter(baselineMs: 0.56, recentMs: 1.46, Detector(minimumIncreaseMs: 0)));

    [Fact]
    public void Assess_TooFewRecentSamples_IsInsufficientData()
    {
        RecordMany(10, 100);
        time.Advance(TimeSpan.FromMinutes(10));
        RecordMany(4, 1_000);
        time.Advance(OneMinute);

        Assert.Equal(AnomalyState.InsufficientData, Assert.Single(detector.Assess()).State);
    }

    [Fact]
    public void Assess_NoBaselineYet_IsInsufficientData()
    {
        RecordMany(10, 1_000);
        time.Advance(OneMinute);

        var weather = Assert.Single(detector.Assess());

        Assert.Equal(new ApiPerformance("weather", AnomalyState.InsufficientData, 10, 1_000, 0, null), weather);
    }

    [Fact]
    public void Assess_TheMinuteInProgress_CountsOnlyOnceItIsComplete()
    {
        RecordMany(10, 100);
        time.Advance(TimeSpan.FromMinutes(10));
        RecordMany(5, 1_000);

        var whileInProgress = Assert.Single(detector.Assess());
        time.Advance(OneMinute);
        var onceComplete = Assert.Single(detector.Assess());

        Assert.Equal((AnomalyState.InsufficientData, 0L, 10L), (whileInProgress.State, whileInProgress.RecentCount, whileInProgress.BaselineCount));
        Assert.Equal((AnomalyState.Anomalous, 5L, 10L), (onceComplete.State, onceComplete.RecentCount, onceComplete.BaselineCount));
    }

    [Fact]
    public void Assess_Baseline_LeavesOutTheWindowAndTheMinuteInProgress()
    {
        RecordMany(10, 100);
        time.Advance(TimeSpan.FromMinutes(10));
        RecordMany(5, 200);
        time.Advance(OneMinute);
        RecordMany(5, 10_000);

        var weather = Assert.Single(detector.Assess());

        Assert.Equal((10L, 100.0), (weather.BaselineCount, weather.BaselineAverageMs));
    }

    [Fact]
    public void Assess_MonitoredApiWithoutCalls_IsListedWithoutAverages() =>
        Assert.Equal(new ApiPerformance("weather", AnomalyState.InsufficientData, 0, null, 0, null), Assert.Single(detector.Assess()));

    private AnomalyDetector Detector(double minimumIncreaseMs) =>
        new(store, Options.Create(new AnomalyOptions { WindowMinutes = 5, Ratio = 1.5, MinSamples = 5, MinimumIncreaseMs = minimumIncreaseMs }));

    private AnomalyState StateAfter(double baselineMs, double recentMs, AnomalyDetector? other = null)
    {
        RecordMany(10, baselineMs);
        time.Advance(TimeSpan.FromMinutes(10));
        RecordMany(5, recentMs);
        time.Advance(OneMinute);
        return Assert.Single((other ?? detector).Assess()).State;
    }

    private void RecordMany(int count, double milliseconds)
    {
        for (var i = 0; i < count; i++)
            store.Record("weather", TimeSpan.FromMilliseconds(milliseconds), succeeded: true);
    }
}
