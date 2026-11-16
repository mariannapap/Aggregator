using System.Text.Json.Serialization;

namespace Aggregator.Api.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<SourceStatusResponse>))]
public enum SourceStatusResponse
{
    [JsonStringEnumMemberName("ok")]
    Ok,

    [JsonStringEnumMemberName("partial")]
    Partial,

    [JsonStringEnumMemberName("no_data")]
    NoData,

    [JsonStringEnumMemberName("stale")]
    Stale,

    [JsonStringEnumMemberName("unavailable")]
    Unavailable
}
