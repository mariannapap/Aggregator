using Aggregator.Core.Model;
using Aggregator.Core.Sources;
using Microsoft.Extensions.Logging;

namespace Aggregator.Infrastructure.OpenMeteo;

internal sealed class AirQualitySource(HttpClient http, ILogger<AirQualitySource> logger) : IExternalSource<LocationQuery, AirQuality>
{
    private const string UsAqi = "us_aqi";

    private static readonly OpenMeteoEndpoint Endpoint = new(
        SourceNames.AirQuality,
        "v1/air-quality",
        HourlyCategory.AirQuality,
        [new(UsAqi, "usAqi", "USAQI")]);

    private readonly OpenMeteoLocationClient client = new(http, logger);

    public string Name => SourceNames.AirQuality;

    public Task<SourceResult<AirQuality>> FetchAsync(LocationQuery query, CancellationToken cancellationToken) =>
        client.FetchAsync(
            Endpoint,
            query,
            values => new CurrentAirQuality((int)Math.Round(values[UsAqi], MidpointRounding.AwayFromZero)),
            (current, hourly) => new AirQuality(current, hourly),
            cancellationToken);
}
