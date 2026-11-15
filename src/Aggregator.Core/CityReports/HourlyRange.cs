namespace Aggregator.Core.CityReports;

public sealed record HourlyRange(DateTimeOffset? RequestedFrom, DateTimeOffset? RequestedTo, DateTimeOffset AppliedFrom, DateTimeOffset AppliedTo)
{
    public bool Clamped =>
        (RequestedFrom is { } from && from != AppliedFrom) || (RequestedTo is { } to && to != AppliedTo);
}
