namespace Aggregator.Api.Contracts;

public sealed record RegionFilterResponse(string Requested, IReadOnlyList<string> Matched, bool Applied);
