using Aggregator.Core.Model;

namespace Aggregator.Core.CityReports;

internal static class HourlyFilter
{
    // Bounds are inclusive; a missing bound means the window's own edge.
    public static bool Overlaps(HourlyOptions hourly, HourlyWindow window) =>
        (hourly.From ?? window.Start) <= window.LastHour && (hourly.To ?? window.LastHour) >= window.Start;

    public static HourlyRange ResolveRange(HourlyOptions hourly, HourlyWindow window) =>
        new(
            hourly.From,
            hourly.To,
            Clamp(hourly.From ?? window.Start, window),
            Clamp(hourly.To ?? window.LastHour, window));

    public static List<HourlyReading> Apply(IEnumerable<HourlyReading> readings, HourlyRange range, bool descending)
    {
        var inRange = readings.Where(reading => reading.Time >= range.AppliedFrom && reading.Time <= range.AppliedTo);
        var byTime = descending
            ? inRange.OrderByDescending(reading => reading.Time)
            : inRange.OrderBy(reading => reading.Time);

        return byTime
            .ThenBy(reading => reading.Category)
            .ThenBy(reading => reading.Metric, StringComparer.Ordinal)
            .ToList();
    }

    private static DateTimeOffset Clamp(DateTimeOffset value, HourlyWindow window)
    {
        var utc = value.ToUniversalTime();
        if (utc < window.Start)
            return window.Start;

        return utc > window.LastHour ? window.LastHour : utc;
    }
}
