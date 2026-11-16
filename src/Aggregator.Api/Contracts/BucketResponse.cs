using System.Text.Json.Serialization;

namespace Aggregator.Api.Contracts;

public sealed record BucketResponse(long Count, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] double? AverageMs);
