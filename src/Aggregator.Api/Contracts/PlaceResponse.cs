namespace Aggregator.Api.Contracts;

public sealed record PlaceResponse(
    string City,
    string Country,
    string? Region,
    CoordinatesResponse Coordinates,
    double DistanceKm,
    WeatherResponse? Weather,
    AirQualityResponse? AirQuality,
    IReadOnlyList<HourlyReadingResponse>? Hourly,
    IReadOnlyDictionary<string, string> Sources);
