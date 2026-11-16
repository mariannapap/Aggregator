namespace Aggregator.Api.Contracts;

/// <summary>Current weather from Open-Meteo.</summary>
/// <param name="Temperature">Air temperature 2 m above ground, in °C.</param>
/// <param name="WindSpeed">Wind speed 10 m above ground, in km/h.</param>
public sealed record WeatherResponse(double Temperature, double WindSpeed);
