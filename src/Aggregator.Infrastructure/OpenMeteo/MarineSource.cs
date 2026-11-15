using Aggregator.Core.Model;
using Aggregator.Core.Sources;
using Microsoft.Extensions.Logging;

namespace Aggregator.Infrastructure.OpenMeteo;

internal sealed class MarineSource(HttpClient http, ILogger<MarineSource> logger) : IExternalSource<LocationQuery, Marine>
{
    private const string WaveHeight = "wave_height";
    private const string SeaSurfaceTemperature = "sea_surface_temperature";

    private static readonly OpenMeteoEndpoint Endpoint = new(
        SourceNames.Marine,
        "v1/marine",
        HourlyCategory.Marine,
        [new(WaveHeight, "waveHeight", "m"), new(SeaSurfaceTemperature, "seaSurfaceTemperature", "°C")]);

    private readonly OpenMeteoLocationClient client = new(http, logger);

    public string Name => SourceNames.Marine;

    public Task<SourceResult<Marine>> FetchAsync(LocationQuery query, CancellationToken cancellationToken) =>
        client.FetchAsync(
            Endpoint,
            query,
            values => new CurrentMarine(values[WaveHeight], values[SeaSurfaceTemperature]),
            (current, hourly) => new Marine(current, hourly),
            cancellationToken);
}
