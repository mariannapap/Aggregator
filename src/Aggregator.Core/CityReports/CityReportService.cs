using Aggregator.Core.Model;
using Aggregator.Core.Query;
using Aggregator.Core.Sources;
using Microsoft.Extensions.Options;

namespace Aggregator.Core.CityReports;

public sealed class CityReportService(
    IExternalSource<GeocodingQuery, IReadOnlyList<Place>> geocoding,
    IExternalSource<LocationQuery, Weather> weather,
    IExternalSource<LocationQuery, AirQuality> airQuality,
    RegionMatcher regionMatcher,
    RelevanceCalculator relevance,
    TimeProvider timeProvider,
    IOptions<CityReportOptions> options)
{
    public async Task<CityReportOutcome> GetAsync(CityReportRequest request, CancellationToken cancellationToken)
    {
        var geocoded = await geocoding.FetchAsync(new GeocodingQuery(request.City, request.Country), cancellationToken);
        if (geocoded.Status == SourceStatus.Unavailable)
            return new CityReportOutcome.GeocodingUnavailable();
        if (geocoded.Value is not { Count: > 0 } places)
            return new CityReportOutcome.CityNotFound();

        var (matchingPlaces, regionMatch) = FilterByRegion(places, request.Region);

        var window = request.Sections.HasFlag(ReportSections.Hourly)
            ? HourlyWindow.StartingAtCurrentHour(timeProvider.GetUtcNow(), options.Value.HourlyWindowHours)
            : null;
        var hourlyRange = window is null ? null : HourlyFilter.ResolveRange(request.Hourly, window);

        var reports = await Task.WhenAll(matchingPlaces.Select(place =>
            BuildPlaceReportAsync(place, geocoded.Status, request, window, hourlyRange, cancellationToken)));

        return new CityReportOutcome.Found(
            new CityReport(new ReportFilters(regionMatch, hourlyRange), PlaceSorter.Sort(reports, request.Sort)));
    }

    private (IReadOnlyList<Place> Places, RegionMatch? Match) FilterByRegion(IReadOnlyList<Place> places, string? region)
    {
        if (string.IsNullOrWhiteSpace(region))
            return (places, null);

        var match = regionMatcher.Match(region, places.Select(place => place.Region));
        if (!match.Applied)
            return (places, match);

        return (places.Where(place => place.Region is not null && match.Matched.Contains(place.Region)).ToList(), match);
    }

    private async Task<PlaceReport> BuildPlaceReportAsync(
        Place place,
        SourceStatus geocodingStatus,
        CityReportRequest request,
        HourlyWindow? window,
        HourlyRange? hourlyRange,
        CancellationToken cancellationToken)
    {
        var weatherQuery = CreateQuery(place, request, ReportSections.Weather, HourlyCategory.Weather, window);
        var airQualityQuery = CreateQuery(place, request, ReportSections.AirQuality, HourlyCategory.AirQuality, window);

        var weatherTask = weatherQuery is null ? Task.FromResult<SourceResult<Weather>?>(null) : FetchAsync(weather, weatherQuery, cancellationToken);
        var airQualityTask = airQualityQuery is null ? Task.FromResult<SourceResult<AirQuality>?>(null) : FetchAsync(airQuality, airQualityQuery, cancellationToken);
        await Task.WhenAll(weatherTask, airQualityTask);

        var weatherResult = await weatherTask;
        var airQualityResult = await airQualityTask;

        var hourly = hourlyRange is null
            ? null
            : HourlyFilter.Apply(
                (weatherResult?.Value?.Hourly ?? []).Concat(airQualityResult?.Value?.Hourly ?? []),
                hourlyRange,
                request.Hourly.SortDescending);

        return new PlaceReport(place, relevance.DistanceKm(place.Coordinates), geocodingStatus, weatherResult, airQualityResult, hourly);
    }

    private LocationQuery? CreateQuery(
        Place place,
        CityReportRequest request,
        ReportSections currentSection,
        HourlyCategory category,
        HourlyWindow? window)
    {
        var includeCurrent = request.Sections.HasFlag(currentSection);
        var includeHourly = window is not null && (request.Hourly.Category is null || request.Hourly.Category == category);
        if (!includeCurrent && !includeHourly)
            return null;

        return new LocationQuery(place.Coordinates, options.Value.CoordinateDecimals, includeCurrent, includeHourly ? window : null);
    }

    private static async Task<SourceResult<TResult>?> FetchAsync<TResult>(
        IExternalSource<LocationQuery, TResult> source,
        LocationQuery query,
        CancellationToken cancellationToken)
        where TResult : class =>
        await source.FetchAsync(query, cancellationToken);
}
