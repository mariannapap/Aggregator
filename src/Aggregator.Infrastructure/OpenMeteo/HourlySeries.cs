using Aggregator.Core.Model;

namespace Aggregator.Infrastructure.OpenMeteo;

internal static class HourlySeries
{
    public static PivotResult Pivot(IReadOnlyList<string>? times, HourlyWindow window, HourlyCategory category, IReadOnlyList<MetricSeries> series)
    {
        if (times is null)
            return new PivotResult([], false);

        var readings = new List<HourlyReading>();
        var complete = true;
        foreach (var metric in series)
        {
            var found = 0;
            for (var index = 0; index < times.Count; index++)
            {
                var time = OpenMeteoHttp.ParseTime(times[index]);
                if (!window.Contains(time))
                    continue;

                var value = metric.Values is not null && index < metric.Values.Count ? metric.Values[index] : null;
                if (value is null)
                    continue;

                readings.Add(new HourlyReading(time, category, metric.Metric, value.Value, metric.Unit));
                found++;
            }

            complete &= found == window.Hours;
        }

        return new PivotResult(readings, complete);
    }

    public static string Unit(IReadOnlyDictionary<string, string>? units, string key, string fallback) =>
        units is not null && units.TryGetValue(key, out var unit) ? unit : fallback;
}
