using Microsoft.Extensions.Logging;

namespace Aggregator.Infrastructure.Caching;

internal static partial class CachingLog
{
    [LoggerMessage(EventId = 2101, Level = LogLevel.Error, Message = "Fetching {Key} failed unexpectedly; serving the last good value if there is one")]
    public static partial void UnexpectedFetchFailure(this ILogger logger, string key, Exception exception);
}
