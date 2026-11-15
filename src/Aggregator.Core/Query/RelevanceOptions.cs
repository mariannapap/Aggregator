using System.ComponentModel.DataAnnotations;

namespace Aggregator.Core.Query;

public sealed class RelevanceOptions
{
    public const string SectionName = "Relevance";

    [Range(-90.0, 90.0)]
    public double ReferenceLatitude { get; set; }

    [Range(-180.0, 180.0)]
    public double ReferenceLongitude { get; set; }
}
