using System.ComponentModel.DataAnnotations;

namespace Aggregator.Core.Statistics;

public sealed class AnomalyOptions
{
    public const string SectionName = "Anomaly";

    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(1);

    [Range(1, 1440)]
    public int WindowMinutes { get; set; } = 5;

    [Range(1.0, 100.0)]
    public double Ratio { get; set; } = 1.5;

    [Range(1, int.MaxValue)]
    public int MinSamples { get; set; } = 5;
}
