using System.Text.Json.Serialization;

namespace Aggregator.Api.Contracts;

/// <summary>insufficient_data: too few attempts to judge. normal: not slower than usual. anomalous: slower than usual by the anomaly rules.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AnomalyStateResponse>))]
public enum AnomalyStateResponse
{
    [JsonStringEnumMemberName("insufficient_data")]
    InsufficientData,

    [JsonStringEnumMemberName("normal")]
    Normal,

    [JsonStringEnumMemberName("anomalous")]
    Anomalous
}
