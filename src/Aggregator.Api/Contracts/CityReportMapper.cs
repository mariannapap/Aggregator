using Aggregator.Core.CityReports;
using Aggregator.Core.Model;
using Aggregator.Core.Sources;

namespace Aggregator.Api.Contracts;

internal static class CityReportMapper
{
    public static CityReportResponse ToResponse(this CityReport report)
    {
        var places = report.Places.Select(ToResponse).ToList();
        return new CityReportResponse(
            new FiltersResponse(
                report.Filters.Region is { } region ? new RegionFilterResponse(region.Requested, region.Matched, region.Applied) : null,
                report.Filters.HourlyRange is { } range
                    ? new HourlyRangeResponse(range.RequestedFrom, range.RequestedTo, range.AppliedFrom, range.AppliedTo, range.Clamped)
                    : null,
                report.Filters.TotalPlaces,
                report.Filters.Truncated),
            places.Any(place => place.Sources.Values.Any(source => IsDegraded(source.Status))),
            places);
    }

    public static SourceStatusResponse ToContract(this SourceStatus status) => status switch
    {
        SourceStatus.Ok => SourceStatusResponse.Ok,
        SourceStatus.Partial => SourceStatusResponse.Partial,
        SourceStatus.NoData => SourceStatusResponse.NoData,
        SourceStatus.Stale => SourceStatusResponse.Stale,
        _ => SourceStatusResponse.Unavailable
    };

    // no_data is a valid answer (e.g. marine inland), not a degradation.
    private static bool IsDegraded(SourceStatusResponse status) =>
        status is SourceStatusResponse.Partial or SourceStatusResponse.Stale or SourceStatusResponse.Unavailable;

    private static PlaceResponse ToResponse(PlaceReport report)
    {
        var sources = new Dictionary<string, SourceResponse>
        {
            [SourceNames.Geocoding] = new(report.Geocoding.Status.ToContract(), report.Geocoding.FetchedAt)
        };
        if (report.Weather is { } weather)
            sources[SourceNames.Weather] = ToResponse(weather);
        if (report.AirQuality is { } airQuality)
            sources[SourceNames.AirQuality] = ToResponse(airQuality);
        if (report.Marine is { } marine)
            sources[SourceNames.Marine] = ToResponse(marine);
        if (report.Hazards is { } hazards)
            sources[SourceNames.Hazards] = ToResponse(hazards);

        return new PlaceResponse(
            report.Place.Name,
            report.Place.Country,
            report.Place.Region,
            new CoordinatesResponse(report.Place.Coordinates.Latitude, report.Place.Coordinates.Longitude),
            Math.Round(report.DistanceKm, 1),
            report.Weather?.Value?.Current is { } currentWeather ? new WeatherResponse(currentWeather.Temperature, currentWeather.WindSpeed) : null,
            report.AirQuality?.Value?.Current is { } currentAirQuality ? new AirQualityResponse(currentAirQuality.UsAqi) : null,
            report.Marine?.Value?.Current is { } currentMarine ? new MarineResponse(currentMarine.WaveHeight, currentMarine.SeaSurfaceTemperature) : null,
            report.Hazards?.Value?.Events.Select(hazard => new HazardResponse(hazard.Title, hazard.Category, hazard.Date, Math.Round(hazard.DistanceKm, 1))).ToList(),
            report.Hourly?.Select(ToResponse).ToList(),
            sources);
    }

    private static SourceResponse ToResponse<T>(SourceResult<T> result)
        where T : class =>
        new(result.Status.ToContract(), result.FetchedAt);

    private static HourlyReadingResponse ToResponse(HourlyReading reading) =>
        new(reading.Time, ToContract(reading.Category), reading.Metric, reading.Value, reading.Unit);

    private static string ToContract(HourlyCategory category) => category switch
    {
        HourlyCategory.Weather => SourceNames.Weather,
        HourlyCategory.AirQuality => SourceNames.AirQuality,
        HourlyCategory.Marine => SourceNames.Marine,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
    };
}
