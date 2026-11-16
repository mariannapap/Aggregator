namespace Aggregator.Core.Model;

public sealed record SourceState(SourceStatus Status, DateTimeOffset? FetchedAt);
