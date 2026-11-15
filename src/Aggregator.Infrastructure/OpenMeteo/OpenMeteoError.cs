using System.Text.Json.Serialization;

namespace Aggregator.Infrastructure.OpenMeteo;

internal sealed record OpenMeteoError([property: JsonPropertyName("reason")] string? Reason);
