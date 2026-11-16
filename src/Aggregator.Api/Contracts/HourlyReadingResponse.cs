namespace Aggregator.Api.Contracts;

/// <summary>One forecast value for one hour.</summary>
/// <param name="Time">The start of the hour, in UTC.</param>
/// <param name="Category">The section the value comes from: weather, airQuality or marine.</param>
/// <param name="Metric">temperature or windSpeed (weather), usAqi (airQuality), waveHeight or seaSurfaceTemperature (marine).</param>
/// <param name="Value">The forecast value, in Unit.</param>
/// <param name="Unit">The unit Open-Meteo reported for the metric, e.g. °C, km/h, m or USAQI.</param>
public sealed record HourlyReadingResponse(DateTimeOffset Time, string Category, string Metric, double Value, string Unit);
