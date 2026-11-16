using Microsoft.Extensions.Options;

namespace Aggregator.Core.Statistics;

public sealed class AnomalyDetector(IStatisticsReader statistics, IOptions<AnomalyOptions> options) : IAnomalyDetector
{
    public IReadOnlyList<ApiPerformance> Assess()
    {
        var settings = options.Value;
        return statistics.Apis.Select(api => Assess(api, settings)).ToList();
    }

    private ApiPerformance Assess(string api, AnomalyOptions settings)
    {
        // Recent first: a request is added to the total before its minute, so the total read afterwards always contains it.
        var recent = statistics.Recent(api, settings.WindowMinutes);
        var total = statistics.Total(api);
        var window = recent.Window;
        var baseline = new WindowSample(
            total.Count - window.Count - recent.CurrentMinute.Count,
            total.TotalMs - window.TotalMs - recent.CurrentMinute.TotalMs);

        var state = AnomalyState.InsufficientData;
        if (window.Count >= settings.MinSamples && baseline.Count >= settings.MinSamples)
        {
            var relativelySlower = window.AverageMs > baseline.AverageMs * settings.Ratio;
            var noticeablySlower = window.AverageMs - baseline.AverageMs >= settings.MinimumIncreaseMs;
            state = relativelySlower && noticeablySlower ? AnomalyState.Anomalous : AnomalyState.Normal;
        }

        return new ApiPerformance(api, state, window.Count, Average(window), baseline.Count, Average(baseline));
    }

    private static double? Average(WindowSample sample) => sample.Count == 0 ? null : Math.Round(sample.AverageMs, 2);
}
