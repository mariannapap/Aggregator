namespace Aggregator.Api.Contracts;

public sealed record SourceResponse(SourceStatusResponse Status, DateTimeOffset? FetchedAt);
