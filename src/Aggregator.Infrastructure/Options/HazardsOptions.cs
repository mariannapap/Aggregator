using System.ComponentModel.DataAnnotations;

namespace Aggregator.Infrastructure.Options;

internal sealed class HazardsOptions
{
    public const string SectionName = "Hazards";

    [Range(1, 1000)]
    public int RadiusKm { get; set; } = 150;

    // EONET leaves some events "open" for years; only events seen in the last Days are kept.
    [Range(1, 365)]
    public int Days { get; set; } = 30;

    [Range(1, 500)]
    public int Limit { get; set; } = 50;
}
