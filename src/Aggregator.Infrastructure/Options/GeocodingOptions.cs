using System.ComponentModel.DataAnnotations;

namespace Aggregator.Infrastructure.Options;

public sealed class GeocodingOptions
{
    public const string SectionName = "Geocoding";

    [Range(1, 100)]
    public int Count { get; set; } = 100;

    [Required]
    public string Language { get; set; } = "en";

    [MinLength(1)]
    public IReadOnlyList<string> AllowedFeatureCodes { get; set; } = [];
}
