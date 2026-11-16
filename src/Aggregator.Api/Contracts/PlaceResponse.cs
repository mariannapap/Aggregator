namespace Aggregator.Api.Contracts;

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
