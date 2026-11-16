using System.Diagnostics;

namespace Aggregator.Infrastructure.Caching;

internal static class CacheTelemetry
{
    public const string SourceName = "Aggregator.Caching";

    public static readonly ActivitySource Source = new(SourceName);
}
