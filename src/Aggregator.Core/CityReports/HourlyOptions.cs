using Aggregator.Core.Model;

namespace Aggregator.Core.CityReports;

public sealed record HourlyOptions(HourlyCategory? Category, DateTimeOffset? From, DateTimeOffset? To, bool SortDescending)
{
    public static HourlyOptions Default { get; } = new(null, null, null, false);
}
