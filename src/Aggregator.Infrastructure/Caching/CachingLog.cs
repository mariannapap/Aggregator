using Microsoft.Extensions.Logging;

namespace Aggregator.Infrastructure.Caching;

internal static partial class CachingLog
{
    [LoggerMessage(EventId = 2101, Level = LogLevel.Error, Message = "Fetching {Key} failed unexpectedly; serving the last good value if there is one")]
    public static partial void UnexpectedFetchFailure(this ILogger logger, string key, Exception exception);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Warning, Message = "Fetching {Key} took longer than its {Budget} budget; serving the last good value if there is one")]
    public static partial void BudgetExceeded(this ILogger logger, string key, TimeSpan budget);
}
