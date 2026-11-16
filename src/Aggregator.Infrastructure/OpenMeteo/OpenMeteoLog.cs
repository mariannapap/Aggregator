using Microsoft.Extensions.Logging;

namespace Aggregator.Infrastructure.OpenMeteo;

internal static partial class OpenMeteoLog
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning, Message = "Call to {Source} failed")]
    public static partial void CallFailed(this ILogger logger, string source, Exception exception);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning, Message = "{Source} rejected the request: {Reason}")]
    public static partial void RequestRejected(this ILogger logger, string source, string? reason);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Information, Message = "{Source} returned incomplete data: {Detail}")]
    public static partial void IncompleteData(this ILogger logger, string source, string detail);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Warning, Message = "{Source} returned {Count} result(s) without coordinates; they were skipped")]
    public static partial void ResultsWithoutCoordinates(this ILogger logger, string source, int count);
}
