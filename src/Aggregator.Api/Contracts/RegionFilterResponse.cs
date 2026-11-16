namespace Aggregator.Api.Contracts;

/// <summary>How the region filter was applied.</summary>
/// <param name="Requested">The region as sent.</param>
/// <param name="Matched">The regions of the found places that matched it.</param>
/// <param name="Applied">False when no region matched: the filter was then ignored and every place is returned.</param>
public sealed record RegionFilterResponse(string Requested, IReadOnlyList<string> Matched, bool Applied);
