using System.ComponentModel.DataAnnotations;

namespace Aggregator.Core.Query;

public sealed class DistanceOptions
{
    public const string SectionName = "Distance";

    [Range(-90.0, 90.0)]
    public double ReferenceLatitude { get; set; }

    [Range(-180.0, 180.0)]
    public double ReferenceLongitude { get; set; }
}
