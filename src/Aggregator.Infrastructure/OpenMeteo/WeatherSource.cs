using Aggregator.Core.Model;
using Aggregator.Core.Sources;
using Microsoft.Extensions.Logging;

namespace Aggregator.Infrastructure.OpenMeteo;

internal sealed class WeatherSource(HttpClient http, ILogger<WeatherSource> logger) : IExternalSource<LocationQuery, Weather>
{
    private const string Temperature = "temperature_2m";
    private const string WindSpeed = "wind_speed_10m";

    private static readonly OpenMeteoEndpoint Endpoint = new(
        SourceNames.Weather,
        "v1/forecast",
        HourlyCategory.Weather,
        [new(Temperature, "temperature", "°C"), new(WindSpeed, "windSpeed", "km/h")]);

    private readonly OpenMeteoLocationClient client = new(http, logger);

    public string Name => SourceNames.Weather;

    public Task<SourceResult<Weather>> FetchAsync(LocationQuery query, CancellationToken cancellationToken) =>
        client.FetchAsync(
            Endpoint,
            query,
            values => new CurrentWeather(values[Temperature], values[WindSpeed]),
            (current, hourly) => new Weather(current, hourly),
            cancellationToken);
}
