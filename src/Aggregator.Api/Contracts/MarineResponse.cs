namespace Aggregator.Api.Contracts;

/// <summary>Current sea conditions from Open-Meteo; only places near the sea have them.</summary>
/// <param name="WaveHeight">Wave height, in metres.</param>
/// <param name="SeaSurfaceTemperature">Sea surface temperature, in °C.</param>
public sealed record MarineResponse(double WaveHeight, double SeaSurfaceTemperature);
