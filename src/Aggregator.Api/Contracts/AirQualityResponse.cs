namespace Aggregator.Api.Contracts;

/// <summary>Current air quality from Open-Meteo.</summary>
/// <param name="UsAqi">US Air Quality Index, 0–500: 0–50 good, 51–100 moderate, 101–150 unhealthy for sensitive groups, 151–200 unhealthy, 201–300 very unhealthy, above 300 hazardous.</param>
public sealed record AirQualityResponse(int UsAqi);
