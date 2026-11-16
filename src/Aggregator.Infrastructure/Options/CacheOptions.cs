using System.ComponentModel.DataAnnotations;

namespace Aggregator.Infrastructure.Options;

internal sealed class CacheOptions
{
    public const string SectionName = "Cache";

    // HybridCache sizes each in-memory entry by its serialized length in bytes.
    [Range(1, 4096)]
    public int SizeLimitMegabytes { get; set; } = 100;
}
