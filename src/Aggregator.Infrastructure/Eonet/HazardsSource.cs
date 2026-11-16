using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Aggregator.Core.Model;
using Aggregator.Core.Sources;
using Aggregator.Infrastructure.Http;
using Aggregator.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aggregator.Infrastructure.Eonet;

// NASA EONET: open natural events (wildfires, storms, volcanoes, floods) around a place.
internal sealed class HazardsSource(HttpClient http, IOptions<HazardsOptions> options, ILogger<HazardsSource> logger)
    : IExternalSource<AreaQuery, Hazards>
{
    private const double KmPerDegreeOfLatitude = 111.32;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public string Name => SourceNames.Hazards;

    public async Task<SourceResult<Hazards>> FetchAsync(AreaQuery query, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        EventsResponse? body;
        try
        {
            using var response = await http.GetAsync(new Uri(BuildUri(query.Center, settings), UriKind.Relative), cancellationToken);
            response.EnsureSuccessStatusCode();
            body = await response.Content.ReadFromJsonAsync<EventsResponse>(SerializerOptions, cancellationToken);
        }
        // Network, HTTP, timeout, open-circuit, limiter and JSON errors make the source unavailable; only the caller's cancellation propagates.
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            if (ExpectedFailure.Describe(exception) is { } reason)
                logger.CallFailed(Name, reason);
            else
                logger.CallFailedUnexpectedly(Name, exception);

            return SourceResult.Unavailable<Hazards>();
        }

        if (body?.Events is not { } events)
            return SourceResult.Unavailable<Hazards>();

        var nearby = events
            .Select(item => ToHazard(item, query.Center))
            .OfType<Hazard>()
            .Where(hazard => hazard.DistanceKm <= settings.RadiusKm)
            .OrderBy(hazard => hazard.DistanceKm)
            .ToList();

        return nearby.Count == 0 ? SourceResult.NoData<Hazards>() : SourceResult.Ok(new Hazards(nearby));
    }

    // EONET's bbox is west, north, east, south. The box contains the circle; events outside the radius are dropped afterwards.
    internal static string BuildUri(Coordinates center, HazardsOptions settings)
    {
        var latitudeSpan = settings.RadiusKm / KmPerDegreeOfLatitude;
        var longitudeSpan = settings.RadiusKm / (KmPerDegreeOfLatitude * Math.Max(Math.Cos(center.Latitude * Math.PI / 180), 0.01));
        var west = Math.Max(center.Longitude - longitudeSpan, -180);
        var east = Math.Min(center.Longitude + longitudeSpan, 180);
        var north = Math.Min(center.Latitude + latitudeSpan, 90);
        var south = Math.Max(center.Latitude - latitudeSpan, -90);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"api/v3/events?status=open&days={settings.Days}&limit={settings.Limit}&bbox={west:0.####},{north:0.####},{east:0.####},{south:0.####}");
    }

    // The latest geometry is where the event is now; an event without a usable title or position is skipped.
    private static Hazard? ToHazard(EventItem item, Coordinates center)
    {
        var latest = item.Geometry?
            .Select(geometry => PositionOf(geometry) is { } position ? new Sighting(geometry.Date, position) : null)
            .OfType<Sighting>()
            .MaxBy(sighting => sighting.Date);
        if (string.IsNullOrWhiteSpace(item.Title) || latest is null)
            return null;

        var category = item.Categories?.FirstOrDefault(category => !string.IsNullOrWhiteSpace(category.Title))?.Title ?? "Other";
        return new Hazard(item.Title, category, latest.Date, center.DistanceKmTo(latest.Position));
    }

    // GeoJSON order is [longitude, latitude]. A polygon is reduced to the average of its outer ring.
    private static Coordinates? PositionOf(GeometryItem geometry) => geometry.Type switch
    {
        "Point" => PointOf(geometry.Coordinates),
        "Polygon" when geometry.Coordinates.ValueKind == JsonValueKind.Array && geometry.Coordinates.GetArrayLength() > 0 =>
            Average(geometry.Coordinates[0].ValueKind == JsonValueKind.Array
                ? geometry.Coordinates[0].EnumerateArray().Select(PointOf).OfType<Coordinates>().ToList()
                : []),
        _ => null
    };

    private static Coordinates? PointOf(JsonElement pair) =>
        pair.ValueKind == JsonValueKind.Array
        && pair.GetArrayLength() >= 2
        && pair[0].ValueKind == JsonValueKind.Number
        && pair[1].ValueKind == JsonValueKind.Number
            ? new Coordinates(pair[1].GetDouble(), pair[0].GetDouble())
            : null;

    private static Coordinates? Average(List<Coordinates> points) =>
        points.Count == 0 ? null : new Coordinates(points.Average(point => point.Latitude), points.Average(point => point.Longitude));

    private sealed record EventsResponse(IReadOnlyList<EventItem>? Events);

    private sealed record EventItem(string? Title, IReadOnlyList<CategoryItem>? Categories, IReadOnlyList<GeometryItem>? Geometry);

    private sealed record CategoryItem(string? Title);

    private sealed record GeometryItem(DateTimeOffset Date, string? Type, JsonElement Coordinates);

    private sealed record Sighting(DateTimeOffset Date, Coordinates Position);
}
