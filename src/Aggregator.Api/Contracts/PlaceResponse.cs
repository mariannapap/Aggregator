namespace Aggregator.Api.Contracts;

/// <summary>One place that matched the city name, with the data of every requested section.</summary>
/// <param name="City">The place's name, as returned by the geocoder.</param>
/// <param name="Country">ISO 3166-1 alpha-2 country code, upper case.</param>
/// <param name="Region">The first-level administrative region, e.g. Attica; absent when the geocoder has none.</param>
/// <param name="Coordinates">The place's position.</param>
/// <param name="DistanceKm">Distance from the reference point (the Acropolis by default), in km, rounded to 0.1.</param>
/// <param name="Weather">Current weather; absent when not requested or not available.</param>
/// <param name="AirQuality">Current air quality; absent when not requested or not available.</param>
/// <param name="Marine">Current sea conditions; absent when not requested or not available.</param>
/// <param name="Hazards">Open natural events nearby, nearest first; only with include=hazards, and absent when nothing is near (no_data) or the source is unavailable.</param>
/// <param name="Hourly">Hourly forecast rows within filters.hourly; only with include=hourly.</param>
/// <param name="Sources">The status of every API called for this place, keyed by API name (geocoding, weather, airQuality, marine, hazards).</param>
public sealed record PlaceResponse(
    string City,
    string Country,
    string? Region,
    CoordinatesResponse Coordinates,
    double DistanceKm,
    WeatherResponse? Weather,
    AirQualityResponse? AirQuality,
    MarineResponse? Marine,
    IReadOnlyList<HazardResponse>? Hazards,
    IReadOnlyList<HourlyReadingResponse>? Hourly,
    IReadOnlyDictionary<string, SourceResponse> Sources);
