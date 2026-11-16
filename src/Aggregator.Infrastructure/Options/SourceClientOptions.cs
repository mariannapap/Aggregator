using System.ComponentModel.DataAnnotations;

namespace Aggregator.Infrastructure.Options;

internal sealed class SourceClientOptions
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

    // A shorter lifetime for no_data answers, e.g. "city not found"; null keeps CacheTtl.
    [Range(typeof(TimeSpan), "00:00:01", "7.00:00:00")]
    public TimeSpan? NoDataCacheTtl { get; set; }

    // After a failure, serve the last good value without calling the API again for this long; zero always calls.
    [Range(typeof(TimeSpan), "00:00:00", "01:00:00")]
    public TimeSpan FailureCooldown { get; set; } = TimeSpan.FromSeconds(30);
}
