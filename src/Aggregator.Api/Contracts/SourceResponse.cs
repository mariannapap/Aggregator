namespace Aggregator.Api.Contracts;

/// <summary>What one external API contributed to a place.</summary>
/// <param name="Status">ok, partial, no_data, stale or unavailable.</param>
/// <param name="FetchedAt">When the data was fetched from the API; for a stale value, when that older value was fetched. Absent when unavailable.</param>
public sealed record SourceResponse(SourceStatusResponse Status, DateTimeOffset? FetchedAt);
