using Aggregator.Core.Statistics;
using Microsoft.Extensions.Options;

namespace Aggregator.UnitTests.Statistics;

public sealed class StatisticsOptionsValidatorTests
{
    [Theory]
    [InlineData(100, 200, true)]
    [InlineData(150, 150, true)]
    [InlineData(300, 200, false)]
    public void Statistics_FastThresholdMustNotExceedSlow(int fastBelowMs, int slowAboveMs, bool valid) =>
        Assert.Equal(valid, new StatisticsOptionsValidator()
            .Validate(null, new StatisticsOptions { FastBelowMs = fastBelowMs, SlowAboveMs = slowAboveMs }).Succeeded);

    [Fact]
    public void Anomaly_Defaults_AreValid() =>
        Assert.True(ValidateAnomaly(minutesKept: 10, windowMinutes: 5, interval: TimeSpan.FromMinutes(1)).Succeeded);

    [Theory]
    [InlineData(5, 5)]
    [InlineData(10, 15)]
    public void Anomaly_HistoryShorterThanWindowPlusCurrentMinute_Fails(int minutesKept, int windowMinutes)
    {
        var result = ValidateAnomaly(minutesKept, windowMinutes, TimeSpan.FromMinutes(1));

        Assert.True(result.Failed);
        Assert.Contains("RecentMinutesKept", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Anomaly_IntervalLongerThanWindow_Fails()
    {
        var result = ValidateAnomaly(minutesKept: 10, windowMinutes: 5, interval: TimeSpan.FromHours(1));

        Assert.True(result.Failed);
        Assert.Contains("Anomaly:Interval", result.FailureMessage, StringComparison.Ordinal);
    }

    private static ValidateOptionsResult ValidateAnomaly(int minutesKept, int windowMinutes, TimeSpan interval) =>
        new AnomalyOptionsValidator(Options.Create(new StatisticsOptions { RecentMinutesKept = minutesKept }))
            .Validate(null, new AnomalyOptions { WindowMinutes = windowMinutes, Interval = interval });
}
