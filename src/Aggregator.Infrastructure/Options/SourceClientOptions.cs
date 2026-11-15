using System.ComponentModel.DataAnnotations;

namespace Aggregator.Infrastructure.Options;

public sealed class SourceClientOptions
{
    public const string SectionPrefix = "HttpClients";

    [Required]
    public Uri? BaseUrl { get; set; }

    [Range(typeof(TimeSpan), "00:00:01", "7.00:00:00")]
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(15);

    [Range(typeof(TimeSpan), "00:00:01", "7.00:00:00")]
    public TimeSpan HourlyCacheTtl { get; set; } = TimeSpan.FromHours(1);

    [Range(typeof(TimeSpan), "00:00:00", "7.00:00:00")]
    public TimeSpan LastGoodRetention { get; set; } = TimeSpan.FromHours(24);
}
