using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aggregator.Core.Model;
using Aggregator.Core.Sources;
using Microsoft.Extensions.Logging;

namespace Aggregator.Infrastructure.OpenMeteo;

internal sealed class OpenMeteoLocationClient(HttpClient http, ILogger logger)
{
    public async Task<SourceResult<TResult>> FetchAsync<TCurrent, TResult>(
        OpenMeteoEndpoint endpoint,
        LocationQuery query,
        Func<IReadOnlyDictionary<string, double>, TCurrent> mapCurrent,
        Func<TCurrent?, IReadOnlyList<HourlyReading>, TResult> build,
        CancellationToken cancellationToken)
        where TCurrent : class
        where TResult : class
    {
        var response = await OpenMeteoHttp.GetAsync<LocationResponse>(http, BuildUri(endpoint, query), endpoint.Source, logger, cancellationToken);
        if (response.Outcome == OpenMeteoOutcome.Failed)
            return SourceResult.Unavailable<TResult>();
        if (response.Body is not { } body)
            return SourceResult.NoData<TResult>();

        var complete = true;
        TCurrent? current = null;
        if (query.IncludesCurrent)
        {
            var values = ReadCurrent(body.Current, endpoint.Series);
            if (values is null)
                complete = false;
            else
                current = mapCurrent(values);
        }

        IReadOnlyList<HourlyReading> hourly = [];
        if (query.Hourly is { } window)
        {
            var series = endpoint.Series
                .Select(item => new MetricSeries(item.Metric, ReadValues(body.Hourly, item.Field), HourlySeries.Unit(body.HourlyUnits, item.Field, item.DefaultUnit)))
                .ToList();
            var pivot = HourlySeries.Pivot(ReadTimes(body.Hourly), window, endpoint.Category, series);
            hourly = pivot.Readings;
            complete &= pivot.Complete;
        }

        if (current is null && hourly.Count == 0)
            return SourceResult.NoData<TResult>();

        var result = build(current, hourly);
        if (complete)
            return SourceResult.Ok(result);

        logger.IncompleteData(endpoint.Source, "missing current values or hourly rows");
        return SourceResult.Partial(result);
    }

    internal static string BuildUri(OpenMeteoEndpoint endpoint, LocationQuery query)
    {
        var uri = new StringBuilder(endpoint.Path)
            .Append("?latitude=").Append(OpenMeteoHttp.FormatCoordinate(query.Coordinates.Latitude))
            .Append("&longitude=").Append(OpenMeteoHttp.FormatCoordinate(query.Coordinates.Longitude))
            .Append("&timezone=GMT");
        if (query.IncludesCurrent)
            uri.Append("&current=").Append(endpoint.Fields);
        if (query.Hourly is { } window)
        {
            uri.Append("&hourly=").Append(endpoint.Fields)
                .Append("&start_hour=").Append(OpenMeteoHttp.FormatHour(window.Start))
                .Append("&end_hour=").Append(OpenMeteoHttp.FormatHour(window.LastHour));
        }

        return uri.ToString();
    }

    private static Dictionary<string, double>? ReadCurrent(IReadOnlyDictionary<string, JsonElement>? current, IReadOnlyList<OpenMeteoSeries> series)
    {
        if (current is null)
            return null;

        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var item in series)
        {
            if (!current.TryGetValue(item.Field, out var value) || value.ValueKind != JsonValueKind.Number)
                return null;
            values[item.Field] = value.GetDouble();
        }

        return values;
    }

    private static List<string>? ReadTimes(IReadOnlyDictionary<string, JsonElement>? hourly) =>
        hourly is not null && hourly.TryGetValue("time", out var times) && times.ValueKind == JsonValueKind.Array
            ? times.EnumerateArray().Select(time => time.GetString() ?? string.Empty).ToList()
            : null;

    private static List<double?>? ReadValues(IReadOnlyDictionary<string, JsonElement>? hourly, string field) =>
        hourly is not null && hourly.TryGetValue(field, out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().Select(value => value.ValueKind == JsonValueKind.Number ? value.GetDouble() : (double?)null).ToList()
            : null;

    private sealed record LocationResponse(
        IReadOnlyDictionary<string, JsonElement>? Current,
        IReadOnlyDictionary<string, JsonElement>? Hourly,
        [property: JsonPropertyName("hourly_units")] IReadOnlyDictionary<string, string>? HourlyUnits);
}
