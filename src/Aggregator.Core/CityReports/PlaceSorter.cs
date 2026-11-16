namespace Aggregator.Core.CityReports;

internal static class PlaceSorter
{
    public static List<PlaceReport> Sort(IEnumerable<PlaceReport> reports, PlaceSort sort) => sort.Field switch
    {
        PlaceSortField.Distance => By(reports, report => report.DistanceKm, sort.Descending),
        PlaceSortField.Name => ByName(reports, sort.Descending),
        PlaceSortField.Temperature => ByTemperature(reports, sort.Descending),
        _ => By(reports, report => report.MatchRank, sort.Descending)
    };

    // OrderBy is stable, so equal keys keep the incoming (geocoder) order.
    private static List<PlaceReport> By<TKey>(IEnumerable<PlaceReport> reports, Func<PlaceReport, TKey> key, bool descending) =>
        (descending ? reports.OrderByDescending(key) : reports.OrderBy(key)).ToList();

    private static List<PlaceReport> ByName(IEnumerable<PlaceReport> reports, bool descending) =>
        (descending
            ? reports.OrderByDescending(report => report.Place.Name, StringComparer.InvariantCultureIgnoreCase)
            : reports.OrderBy(report => report.Place.Name, StringComparer.InvariantCultureIgnoreCase))
        .ThenBy(report => report.Place.Country, StringComparer.Ordinal)
        .ThenBy(report => report.Place.Region, StringComparer.InvariantCultureIgnoreCase)
        .ToList();

    private static List<PlaceReport> ByTemperature(IEnumerable<PlaceReport> reports, bool descending)
    {
        var withoutTemperatureLast = reports.OrderBy(report => Temperature(report) is null);
        var byTemperature = descending
            ? withoutTemperatureLast.ThenByDescending(Temperature)
            : withoutTemperatureLast.ThenBy(Temperature);

        return byTemperature.ThenBy(report => report.DistanceKm).ToList();
    }

    private static double? Temperature(PlaceReport report) => report.Weather?.Value?.Current?.Temperature;
}
