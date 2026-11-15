using System.ComponentModel.DataAnnotations;

namespace Aggregator.Core.Statistics;

public sealed class StatisticsOptions
{
    public const string SectionName = "Statistics";

    [Range(1, int.MaxValue)]
    public int FastBelowMs { get; set; } = 100;

    [Range(1, int.MaxValue)]
    public int SlowAboveMs { get; set; } = 200;

    [Range(1, 1440)]
    public int RecentMinutesKept { get; set; } = 10;
}
