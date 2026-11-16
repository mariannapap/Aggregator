using System.Text.Json;

namespace Aggregator.Testing;

internal static class ReportJson
{
    public static string? SourceStatus(this JsonElement place, string source) =>
        place.GetProperty("sources").GetProperty(source).GetProperty("status").GetString();
}
