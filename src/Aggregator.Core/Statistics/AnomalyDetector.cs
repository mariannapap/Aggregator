using Microsoft.Extensions.Options;

namespace Aggregator.Core.Statistics;

public sealed class AnomalyDetector(StatisticsStore store, IOptions<AnomalyOptions> options) : IAnomalyDetector
{
    public IReadOnlyList<PerformanceAnomaly> Detect()
    {
        var settings = options.Value;
        var anomalies = new List<PerformanceAnomaly>();

        foreach (var api in store.Apis)
        {
            var total = store.Total(api);
            var recent = store.Recent(api, settings.WindowMinutes);
            var baselineCount = total.Count - recent.Count;
            if (recent.Count < settings.MinSamples || baselineCount < settings.MinSamples)
                continue;

            var baselineAverage = (total.TotalMs - recent.TotalMs) / baselineCount;
            var relativelySlower = recent.AverageMs > baselineAverage * settings.Ratio;
            var noticeablySlower = recent.AverageMs - baselineAverage >= settings.MinimumIncreaseMs;
            if (relativelySlower && noticeablySlower)
                anomalies.Add(new PerformanceAnomaly(api, Math.Round(recent.AverageMs, 2), Math.Round(baselineAverage, 2), recent.Count));
        }

        return anomalies;
    }
}
