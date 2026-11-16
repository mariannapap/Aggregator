using Microsoft.Extensions.Logging;

namespace Aggregator.Infrastructure.Eonet;

internal static partial class HazardsLog
{
    [LoggerMessage(EventId = 2201, Level = LogLevel.Warning, Message = "Call to {Source} failed")]
    public static partial void CallFailed(this ILogger logger, string source, Exception exception);
}
