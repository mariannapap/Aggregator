using Aggregator.Core.CityReports;
using Aggregator.Core.Model;
using Aggregator.Core.Sources;

namespace Aggregator.Api.Contracts;

internal static class CityReportMapper
{
    public static CityReportResponse ToResponse(this CityReport report) =>
        new(
            new FiltersResponse(
                report.Filters.Region is { } region ? new RegionFilterResponse(region.Requested, region.Matched, region.Applied) : null,
                report.Filters.HourlyRange is { } range
                    ? new HourlyRangeResponse(range.RequestedFrom, range.RequestedTo, range.AppliedFrom, range.AppliedTo, range.Clamped)
                    : null),
            report.Places.Select(ToResponse).ToList());

    public static string ToContract(this SourceStatus status) => status switch
    {
        SourceStatus.Ok => "ok",
        SourceStatus.Partial => "partial",
        SourceStatus.NoData => "no_data",
        SourceStatus.Stale => "stale",
        _ => "unavailable"
    };

    private static PlaceResponse ToResponse(PlaceReport report)
    {
        var sources = new Dictionary<string, string> { [SourceNames.Geocoding] = report.GeocodingStatus.ToContract() };
        if (report.Weather is { } weather)
            sources[SourceNames.Weather] = weather.Status.ToContract();
        if (report.AirQuality is { } airQuality)
            sources[SourceNames.AirQuality] = airQuality.Status.ToContract();

        return new PlaceResponse(
            report.Place.Name,
            report.Place.Country,
            report.Place.Region,
            new CoordinatesResponse(report.Place.Coordinates.Latitude, report.Place.Coordinates.Longitude),
            Math.Round(report.DistanceKm, 1),
            report.Weather?.Value?.Current is { } currentWeather ? new WeatherResponse(currentWeather.Temperature, currentWeather.WindSpeed) : null,
            report.AirQuality?.Value?.Current is { } currentAirQuality ? new AirQualityResponse(currentAirQuality.UsAqi) : null,
            report.Hourly?.Select(ToResponse).ToList(),
            sources);
    }

    private static HourlyReadingResponse ToResponse(HourlyReading reading) =>
        new(reading.Time, reading.Category == HourlyCategory.Weather ? SourceNames.Weather : SourceNames.AirQuality, reading.Metric, reading.Value, reading.Unit);
}
