namespace Aggregator.Api.Contracts;

/// <summary>The hourly range that was asked for and the one that was applied.</summary>
/// <param name="RequestedFrom">hourly.from as sent; absent when not sent.</param>
/// <param name="RequestedTo">hourly.to as sent; absent when not sent.</param>
/// <param name="AppliedFrom">The first hour returned: hourly.from clamped to the hourly window, or the window's start.</param>
/// <param name="AppliedTo">The last hour returned: hourly.to clamped to the hourly window, or the window's last hour.</param>
/// <param name="Clamped">True when a requested bound fell outside the window and was moved to its edge.</param>
public sealed record HourlyRangeResponse(
    DateTimeOffset? RequestedFrom,
    DateTimeOffset? RequestedTo,
    DateTimeOffset AppliedFrom,
    DateTimeOffset AppliedTo,
    bool Clamped);
