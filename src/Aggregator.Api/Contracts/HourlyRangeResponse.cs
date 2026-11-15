namespace Aggregator.Api.Contracts;

public sealed record HourlyRangeResponse(
    DateTimeOffset? RequestedFrom,
    DateTimeOffset? RequestedTo,
    DateTimeOffset AppliedFrom,
    DateTimeOffset AppliedTo,
    bool Clamped);
