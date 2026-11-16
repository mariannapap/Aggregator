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
    IExternalSource<AreaQuery, Hazards> hazards,
    RegionMatcher regionMatcher,
    DistanceCalculator distance,
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

        // Relevance and name order come from geocoding alone; only a temperature sort needs the weather of every place first.
        var weatherFirst = request.Sort.Field == PlaceSortField.Temperature;
        var candidates = await ForEachPlaceAsync(matchingPlaces, async (place, token) => new PlaceReport(
            place,
            distance.DistanceKm(place.Coordinates),
            PlaceRanker.Rank(place, request.City),
            new SourceState(geocoded.Status, geocoded.FetchedAt),
            weatherFirst ? await FetchAsync(weather, CreateQuery(place, request, ReportSections.Weather, HourlyCategory.Weather, window), token) : null,
            null,
            null,
            null,
            null), cancellationToken);
        var page = PlaceSorter.Sort(candidates, request.Sort).Skip(request.Page.Offset).Take(request.Page.Limit).ToList();

        var reports = await ForEachPlaceAsync(page, (candidate, token) =>
            CompletePlaceReportAsync(candidate, weatherFirst, request, window, hourlyRange, token), cancellationToken);

        return new CityReportOutcome.Found(
            new CityReport(new ReportFilters(regionMatch, hourlyRange, candidates.Length, page.Count < candidates.Length), reports));
    }

    // At most MaxConcurrentPlaces places in flight, so one request cannot take every permit of the shared per-API limiters; results keep the input order.
    private async Task<PlaceReport[]> ForEachPlaceAsync<TItem>(
        IReadOnlyList<TItem> items,
        Func<TItem, CancellationToken, Task<PlaceReport>> work,
        CancellationToken cancellationToken)
    {
        var results = new PlaceReport[items.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, items.Count),
            new ParallelOptions { MaxDegreeOfParallelism = options.Value.MaxConcurrentPlaces, CancellationToken = cancellationToken },
            async (index, token) => results[index] = await work(items[index], token));
        return results;
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

    private async Task<PlaceReport> CompletePlaceReportAsync(
        PlaceReport candidate,
        bool weatherFetched,
        CityReportRequest request,
        HourlyWindow? window,
        HourlyRange? hourlyRange,
        CancellationToken cancellationToken)
    {
        var place = candidate.Place;
        var weatherTask = weatherFetched
            ? Task.FromResult(candidate.Weather)
            : FetchAsync(weather, CreateQuery(place, request, ReportSections.Weather, HourlyCategory.Weather, window), cancellationToken);
        var airQualityTask = FetchAsync(airQuality, CreateQuery(place, request, ReportSections.AirQuality, HourlyCategory.AirQuality, window), cancellationToken);
        var marineTask = FetchAsync(marine, CreateQuery(place, request, ReportSections.Marine, HourlyCategory.Marine, window), cancellationToken);
        var hazardsTask = FetchHazardsAsync(place, request, cancellationToken);
        await Task.WhenAll(weatherTask, airQualityTask, marineTask, hazardsTask);

        var weatherResult = await weatherTask;
        var airQualityResult = await airQualityTask;
        var marineResult = await marineTask;

        var hourly = hourlyRange is null
            ? null
            : HourlyFilter.Apply(
                [.. weatherResult?.Value?.Hourly ?? [], .. airQualityResult?.Value?.Hourly ?? [], .. marineResult?.Value?.Hourly ?? []],
                hourlyRange,
                request.Hourly.SortDescending);

        return candidate with { Weather = weatherResult, AirQuality = airQualityResult, Marine = marineResult, Hazards = await hazardsTask, Hourly = hourly };
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

    private async Task<SourceResult<Hazards>?> FetchHazardsAsync(Place place, CityReportRequest request, CancellationToken cancellationToken) =>
        request.Sections.HasFlag(ReportSections.Hazards)
            ? await hazards.FetchAsync(new AreaQuery(place.Coordinates, options.Value.CoordinateDecimals), cancellationToken)
            : null;

    private static async Task<SourceResult<TResult>?> FetchAsync<TResult>(
        IExternalSource<LocationQuery, TResult> source,
        LocationQuery? query,
        CancellationToken cancellationToken)
        where TResult : class =>
        query is null ? null : await source.FetchAsync(query, cancellationToken);
}
