using System.Text.Json.Serialization;

namespace Aggregator.Api.Contracts;

/// <summary>Attempts in one duration bucket.</summary>
/// <param name="Count">Attempts in the bucket.</param>
/// <param name="AverageMs">Their average duration in milliseconds; null when the bucket is empty.</param>
public sealed record BucketResponse(long Count, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] double? AverageMs);
