using System.Text.Json.Serialization;

namespace Aggregator.Api.Contracts;

/// <summary>
/// ok: complete data. partial: the API answered with some values missing. no_data: the API answered but had nothing for
/// this place, e.g. marine inland. stale: the API failed, so its last good data is shown (see fetchedAt). unavailable:
/// the API failed and there is no earlier data.
/// </summary>
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
