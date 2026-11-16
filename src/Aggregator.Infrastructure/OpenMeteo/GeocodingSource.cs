using System.Globalization;
using System.Text.Json.Serialization;
using Aggregator.Core.Model;
using Aggregator.Core.Sources;
using Aggregator.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aggregator.Infrastructure.OpenMeteo;

internal sealed class GeocodingSource(HttpClient http, IOptions<GeocodingOptions> options, ILogger<GeocodingSource> logger)
    : IExternalSource<GeocodingQuery, IReadOnlyList<Place>>
{
    public string Name => SourceNames.Geocoding;

    public async Task<SourceResult<IReadOnlyList<Place>>> FetchAsync(GeocodingQuery query, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var uri = string.Create(CultureInfo.InvariantCulture,
            $"v1/search?name={Uri.EscapeDataString(query.City)}&countryCode={query.Country}&count={settings.Count}&language={settings.Language}&format=json");

        var response = await OpenMeteoHttp.GetAsync<GeocodingResponse>(http, uri, Name, logger, cancellationToken);
        if (response.Outcome == OpenMeteoOutcome.Failed)
            return SourceResult.Unavailable<IReadOnlyList<Place>>();
        if (response.Body?.Results is not { Count: > 0 } results)
            return SourceResult.NoData<IReadOnlyList<Place>>();

        var allowed = settings.AllowedFeatureCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matching = results
            .Where(result => result.FeatureCode is not null && allowed.Contains(result.FeatureCode))
            .Where(result => string.Equals(result.CountryCode, query.Country, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var places = matching
            .Where(result => result.Latitude is not null && result.Longitude is not null)
            .Select(result => new Place(result.Name, result.CountryCode!.ToUpperInvariant(), result.Admin1, new Coordinates(result.Latitude!.Value, result.Longitude!.Value)))
            .ToList();
        if (places.Count < matching.Count)
            logger.ResultsWithoutCoordinates(Name, matching.Count - places.Count);

        return places.Count == 0 ? SourceResult.NoData<IReadOnlyList<Place>>() : SourceResult.Ok<IReadOnlyList<Place>>(places);
    }

    private sealed record GeocodingResponse(IReadOnlyList<GeocodingResult>? Results);

    private sealed record GeocodingResult(
        string Name,
        double? Latitude,
        double? Longitude,
        [property: JsonPropertyName("feature_code")] string? FeatureCode,
        [property: JsonPropertyName("country_code")] string? CountryCode,
        string? Admin1);
}
