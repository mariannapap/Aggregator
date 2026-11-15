using System.ComponentModel.DataAnnotations;

namespace Aggregator.Core.Query;

public sealed class RegionMatchingOptions
{
    public const string SectionName = "RegionMatching";

    [Range(0.0, 1.0)]
    public double MaxEditDistanceRatio { get; set; } = 0.3;
}
