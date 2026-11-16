using Aggregator.Core.Model;
using Aggregator.Core.Query;
using Aggregator.Core.Sources;
using Microsoft.Extensions.Options;

namespace Aggregator.Core.CityReports;

public sealed class CityReportService(
    IExternalSource<GeocodingQuery, IReadOnlyList<Place>> geocoding,
    IExternalSource<LocationQuery, Weather> weather,
    IExternalSource<LocationQuery, AirQuality> airQuality,
    IExternalSource<LocationQuery, Marine> marine,
    RegionMatcher regionMatcher,
    RelevanceCalculator relevance,
    TimeProvider timeProvider,
    IOptions<CityReportOptions> options)
{
    public async Task<CityReportOutcome> GetAsync(CityReportRequest request, CancellationToken cancellationToken)
    {
        var window = request.Sections.HasFlag(ReportSections.Hourly)
            ? HourlyWindow.StartingAtCurrentHour(timeProvider.GetUtcNow(), options.Value.HourlyWindowHours)
            : null;
        if (window is not null && !HourlyFilter.Overlaps(request.Hourly, window))
            return new CityReportOutcome.HourlyRangeOutsideWindow(window);

        var geocoded = await geocoding.FetchAsync(new GeocodingQuery(request.City, request.Country), cancellationToken);
        if (geocoded.Status == SourceStatus.Unavailable)
            return new CityReportOutcome.GeocodingUnavailable();
        if (geocoded.Value is not { Count: > 0 } places)
            return new CityReportOutcome.CityNotFound();

        var (matchingPlaces, regionMatch) = FilterByRegion(places, request.Region);
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
        var weatherTask = FetchAsync(weather, CreateQuery(place, request, ReportSections.Weather, HourlyCategory.Weather, window), cancellationToken);
        var airQualityTask = FetchAsync(airQuality, CreateQuery(place, request, ReportSections.AirQuality, HourlyCategory.AirQuality, window), cancellationToken);
        var marineTask = FetchAsync(marine, CreateQuery(place, request, ReportSections.Marine, HourlyCategory.Marine, window), cancellationToken);
        await Task.WhenAll(weatherTask, airQualityTask, marineTask);

        var weatherResult = await weatherTask;
        var airQualityResult = await airQualityTask;
        var marineResult = await marineTask;

        var hourly = hourlyRange is null
            ? null
            : HourlyFilter.Apply(
                [.. weatherResult?.Value?.Hourly ?? [], .. airQualityResult?.Value?.Hourly ?? [], .. marineResult?.Value?.Hourly ?? []],
                hourlyRange,
                request.Hourly.SortDescending);

        return new PlaceReport(
            place,
            relevance.DistanceKm(place.Coordinates),
            geocodingStatus,
            weatherResult,
            airQualityResult,
            marineResult,
            hourly);
    }

    private LocationQuery? CreateQuery(
        Place place,
        CityReportRequest request,
        ReportSections currentSection,
        HourlyCategory category,
        HourlyWindow? window)
    {
        var includeCurrent = request.Sections.HasFlag(currentSection);
        var includeHourly = window is not null && (request.Hourly.Category is { } selected
            ? selected == category
            : request.Sections.IncludesHourlyFor(category));
        if (!includeCurrent && !includeHourly)
            return null;

        return new LocationQuery(place.Coordinates, options.Value.CoordinateDecimals, includeCurrent, includeHourly ? window : null);
    }

    private static async Task<SourceResult<TResult>?> FetchAsync<TResult>(
        IExternalSource<LocationQuery, TResult> source,
        LocationQuery? query,
        CancellationToken cancellationToken)
        where TResult : class =>
        query is null ? null : await source.FetchAsync(query, cancellationToken);
}
